using System.Text.Json;
using TextTools.Core.Formatting;

namespace TextTools.Core.Tests;

public class FormatSettingsTests
{
    public static TheoryData<string> Codes => [.. IdeLanguage.Codes];

    private static Dictionary<string, string> Resource(string name)
    {
        using var stream = typeof(UiTexts).Assembly.GetManifestResourceStream($"TextTools.Core.Ui.{name}.json")!;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
    }

    [Fact]
    public void SettingIdsAreUniqueAndTheDefaultsAreInRange()
    {
        Assert.Equal(FormatSettings.All.Count, FormatSettings.All.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(FormatSettings.All, s => Assert.InRange(s.Default, s.Min, s.Max));
        Assert.Equal(["general", "case", "code", "sql", "wrapping", "blank"], FormatSettings.Tabs);
    }

    [Fact]
    public void TheDefaultProfileIsTheDefaultOptions() => Assert.Equal(FormatOptions.Default, new FormatProfile().ToOptions());

    [Fact]
    public void EveryRuleOptionIsReachableFromASetting()
    {
        // One setting per option: switching each bool setting off changes exactly the option it stands for.
        var baseline = new FormatProfile().ToOptions();
        foreach (var setting in FormatSettings.All.Where(s => s.Kind == SettingKind.Bool))
        {
            var profile = new FormatProfile();
            profile.Set(setting.Id, 0);
            Assert.NotEqual(baseline, profile.ToOptions());
        }
        var margin = new FormatProfile();
        margin.Set("margin", 100);
        Assert.Equal(100, margin.ToOptions().Margin);
    }

    [Fact]
    public void ValuesAreClampedAndUnknownIdsIgnored()
    {
        var profile = new FormatProfile();
        profile.Set("margin", 5);
        Assert.Equal(40, profile.Get("margin"));
        profile.Set("margin", 9999);
        Assert.Equal(200, profile.Get("margin"));
        profile.Set("endWithName", 7);
        Assert.Equal(1, profile.Get("endWithName"));
        profile.Set("nope", 1);
        Assert.Equal(0, profile.Get("nope"));
        Assert.False(profile.IsOn("nope"));
    }

    [Fact]
    public void ProfilesRoundTripThroughJson()
    {
        var profile = new FormatProfile();
        profile.Set("margin", 90);
        profile.Set("alignAssignments", 0);
        var json = profile.ToJson();
        Assert.Contains("\"alignAssignments\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"margin\": 90", json, StringComparison.Ordinal);

        var copy = FormatProfile.FromJson(json);
        Assert.Equal(90, copy.Get("margin"));
        Assert.False(copy.IsOn("alignAssignments"));
        Assert.True(copy.IsOn("endWithName"));
        Assert.Equal(profile.ToOptions(), copy.ToOptions());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"settings\": 5}")]
    [InlineData("{\"settings\": {\"margin\": \"wide\", \"endWithName\": null, \"unknown\": true}}")]
    public void BrokenOrForeignFilesGiveTheDefaultProfile(string? json) => Assert.Equal(FormatOptions.Default, FormatProfile.FromJson(json).ToOptions());

    [Fact]
    public void AnOldFileKeepsNewSettingsAtTheirDefaults()
    {
        var profile = FormatProfile.FromJson("{\"version\":1,\"settings\":{\"margin\":120}}");
        Assert.Equal(120, profile.Get("margin"));
        Assert.True(profile.IsOn("blankLineBeforeSubprograms"));
    }

    [Fact]
    public void CloneIsIndependent()
    {
        var a = new FormatProfile();
        var b = a.Clone();
        b.Set("margin", 99);
        Assert.Equal(76, a.Get("margin"));
        Assert.Equal(99, b.Get("margin"));
    }

    [Fact]
    public void CurrentOptionsDefaultToTheDefaultAndNullFallsBack()
    {
        Assert.Equal(FormatOptions.Default, PostFormatter.Current);
        PostFormatter.Current = null!;
        Assert.Equal(FormatOptions.Default, PostFormatter.Current);
    }

    [Fact]
    public void EverySettingAndTabHasAnEnglishText()
    {
        var english = Resource("texts");
        Assert.All(FormatSettings.All, s => Assert.True(english.ContainsKey("setting." + s.Id), s.Id));
        Assert.All(FormatSettings.Tabs, t => Assert.True(english.ContainsKey("tab." + t), t));
        foreach (var key in new[] { "settings.title", "settings.saved", "button.save", "button.default", "button.close", "preview.title" })
        {
            Assert.True(english.ContainsKey(key), key);
        }
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryLanguageHasTheSameKeysAsEnglishAndSafeTexts(string code)
    {
        var english = Resource("texts");
        var translated = Resource($"texts.{code}");
        Assert.True(english.Keys.Order(StringComparer.Ordinal).SequenceEqual(translated.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal));
        Assert.All(translated.Values, v =>
        {
            Assert.NotEmpty(v);
            Assert.DoesNotContain('[', v);
            Assert.DoesNotContain(']', v);
            Assert.DoesNotContain('&', v);
        });
        // The code words in a label are part of the meaning: they stay as they are in every language.
        Assert.Contains("END name;", translated["setting.endWithName"], StringComparison.Ordinal);
        Assert.Contains("EXECUTE IMMEDIATE", translated["setting.executeImmediateLiteralOnOwnLine"], StringComparison.Ordinal);
        Assert.Contains(":=", translated["setting.alignAssignments"], StringComparison.Ordinal);
        Assert.Contains("%TYPE", translated["setting.attributesUpper"], StringComparison.Ordinal);
        Assert.NotEqual(english["button.save"], translated["button.save"]);
        Assert.Equal(code, UiTexts.For(code).Language);
        Assert.Equal(translated["tab.code"], UiTexts.For(code).Text("tab.code"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("English")]
    [InlineData("Klingon")]
    public void NoTranslationMeansEnglishAndAMissingKeyShowsTheKey(string? language)
    {
        var texts = UiTexts.For(language);
        Assert.Same(UiTexts.English, texts);
        Assert.Equal("Save", texts.Text("button.save"));
        Assert.Equal("no.such.key", texts.Text("no.such.key"));
        Assert.Same(UiTexts.For("Russian"), UiTexts.For("Russian.lang"));
    }

    [Fact]
    public void EverySwitchChangesThePreviewSample()
    {
        var baseline = PostFormatter.Format(FormatSample.Text);
        Assert.NotEqual(FormatSample.Text, baseline);
        foreach (var setting in FormatSettings.All.Where(s => s.Kind == SettingKind.Bool))
        {
            var profile = new FormatProfile();
            profile.Set(setting.Id, 0);
            Assert.True(!string.Equals(baseline, PostFormatter.Format(FormatSample.Text, profile.ToOptions()), StringComparison.Ordinal), setting.Id);
        }
    }
}
