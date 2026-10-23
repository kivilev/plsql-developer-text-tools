using System.Text.Json;

namespace TextTools.Core.Tests;

/// <summary>The plug-in menu speaks every language of the IDE's translations (Preferences: Language).</summary>
public class MenuCaptionsTests
{
    /// <summary>The IDE's translation file names (Languages folder) and the language each one gets.</summary>
    public static TheoryData<string, string> IdeLanguages => new()
    {
        { "Brazilian.lang", "pt" },
        { "Chinese.lang", "zh" },
        { "French.lang", "fr" },
        { "German.lang", "de" },
        { "Hungarian.lang", "hu" },
        { "Japanese.lang", "ja" },
        { "Russian.lang", "ru" },
        { "Spanish.lang", "es" },
    };

    public static TheoryData<string> Codes => [.. IdeLanguage.Codes];

    private static Dictionary<string, string> Resource(string code)
    {
        using var stream = typeof(MenuCaptions).Assembly.GetManifestResourceStream($"TextTools.Core.Menu.captions.{code}.json")!;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(IdeLanguages))]
    public void TheIdeTranslationNameSelectsTheLanguage(string file, string code)
    {
        Assert.Equal(code, IdeLanguage.CodeOf(file));
        Assert.Equal(code, IdeLanguage.CodeOf(file.Replace(".lang", "", StringComparison.Ordinal).ToUpperInvariant()));
        Assert.Equal(code, IdeLanguage.Resolve("", file));
        Assert.Equal(code, MenuCaptions.For("", file).Language);
        Assert.Equal(code, MenuCaptions.For(code).Language);
        Assert.Contains(code, IdeLanguage.Codes);
    }

    [Theory]
    [InlineData("Русский", "ru")]
    [InlineData("Español", "es")]
    [InlineData("Português (Brasil)", "pt")]
    [InlineData("中文", "zh")]
    [InlineData("日本語", "ja")]
    [InlineData("Deutsch", "de")]
    [InlineData("Français", "fr")]
    [InlineData("Magyar", "hu")]
    public void NativeLanguageNamesAreRecognized(string name, string code) => Assert.Equal(code, IdeLanguage.CodeOf(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("English")]
    [InlineData("Klingon")]
    public void NoTranslationOrAnUnknownOneMeansEnglish(string? name)
    {
        Assert.Null(IdeLanguage.CodeOf(name));
        Assert.Same(MenuCaptions.English, MenuCaptions.For(name));
        Assert.Equal("", MenuCaptions.English.Language);
        Assert.Equal("Sort A-Z", MenuCaptions.English.Caption("Sort A-Z"));
    }

    [Fact]
    public void CaptionsAreCachedPerLanguageAndAMissingOneStaysEnglish()
    {
        Assert.Same(MenuCaptions.For("Russian"), MenuCaptions.For("Russian.lang"));
        Assert.Equal("no such caption", MenuCaptions.For("Russian").Caption("no such caption"));
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryMenuNameAndCommandIsTranslatedSafely(string code)
    {
        var file = Resource(code);
        var names = Commands.All.Select(c => c.Menu).Concat(Commands.All.Select(c => c.Name)).Distinct(StringComparer.Ordinal).ToList();

        Assert.All(names, n => Assert.True(file.TryGetValue(n, out var t) && t.Length > 0, $"{code}: no translation for {n}")); // nothing left to the English fallback
        Assert.All(file.Values, v =>
        {
            Assert.DoesNotContain('[', v); // the IDE cuts [...] out of a caption
            Assert.DoesNotContain(']', v);
            Assert.DoesNotContain('&', v); // accelerator mark
            Assert.Equal(v.Trim(), v);
        });
        var captions = MenuCaptions.For(code);
        Assert.Equal(code, captions.Language);
        foreach (var menu in Commands.All.GroupBy(c => c.Menu, StringComparer.Ordinal))
        {
            var shown = menu.Select(c => captions.Caption(c.Name)).ToList();
            Assert.Equal(shown.Count, shown.Distinct(StringComparer.Ordinal).Count()); // two items of a menu must not read the same
        }
        var menus = Commands.All.Select(c => captions.Caption(c.Menu)).Distinct(StringComparer.Ordinal).ToList();
        Assert.Equal(Commands.All.Select(c => c.Menu).Distinct(StringComparer.Ordinal).Count(), menus.Count);
        Assert.NotEqual("SQL Tools", captions.Caption("SQL Tools") + "?"); // sanity: lookup returns text
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryLanguageFileHasTheSameKeysAsEnglish(string code)
    {
        var english = Resource("ru").Keys; // the key set is the English captions; all files must agree
        Assert.True(english.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(Resource(code).Keys.OrderBy(k => k, StringComparer.Ordinal), StringComparer.Ordinal));
    }
}
