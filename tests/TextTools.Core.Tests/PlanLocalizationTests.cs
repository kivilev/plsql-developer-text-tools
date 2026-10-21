using System.Text.Json;
using System.Text.RegularExpressions;
using TextTools.Core.Plan;

namespace TextTools.Core.Tests;

/// <summary>The Extended Explain Plan speaks every language of the IDE's translations (Preferences: Language).</summary>
public partial class PlanLocalizationTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 21, 4, 51, 0, TimeSpan.Zero);

    /// <summary>The IDE's translation file names (Languages folder) and the report language each one gets.</summary>
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

    public static TheoryData<string> Codes => [.. PlanReference.LanguageCodes];

    [GeneratedRegex(@"\{[A-Za-z]+\}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder();

    private static JsonElement Resource(string name)
    {
        using var stream = typeof(PlanReference).Assembly.GetManifestResourceStream($"TextTools.Core.Plan.{name}.json")!;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.Clone();
    }

    private static IEnumerable<(string Path, string Text)> Texts(JsonElement root, bool translation)
    {
        foreach (var section in new[] { "columns", "banners", "ui" })
        {
            foreach (var p in root.GetProperty(section).EnumerateObject()) yield return ($"{section}.{p.Name}", p.Value.GetString()!);
        }
        foreach (var p in root.GetProperty("operations").EnumerateObject())
        {
            foreach (var f in new[] { "what", "normal", "suspicious", "check" }) yield return ($"operations.{p.Name}.{f}", p.Value.GetProperty(f).GetString()!);
        }
        foreach (var p in root.GetProperty("rules").EnumerateObject())
        {
            foreach (var f in new[] { "title", "detail", "normal" }) yield return ($"rules.{p.Name}.{f}", p.Value.GetProperty(f).GetString()!);
        }
        if (translation)
        {
            foreach (var p in root.GetProperty("notes").EnumerateObject())
            {
                foreach (var f in new[] { "meaning", "action" }) yield return ($"notes.{p.Name}.{f}", p.Value.GetProperty(f).GetString()!);
            }
        }
        else
        {
            foreach (var n in root.GetProperty("notes").EnumerateArray())
            {
                foreach (var f in new[] { "meaning", "action" }) yield return ($"notes.{n.GetProperty("match").GetString()}.{f}", n.GetProperty(f).GetString()!);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryTranslationHasTheSameKeysAndPlaceholdersAsEnglish(string code)
    {
        var english = Texts(Resource("reference"), translation: false).ToDictionary(t => t.Path, t => t.Text, StringComparer.Ordinal);
        var translated = Texts(Resource($"reference.{code}"), translation: true).ToDictionary(t => t.Path, t => t.Text, StringComparer.Ordinal);

        Assert.Empty(english.Keys.Except(translated.Keys, StringComparer.Ordinal));
        Assert.Empty(translated.Keys.Except(english.Keys, StringComparer.Ordinal));
        foreach (var (path, text) in english)
        {
            var other = translated[path];
            Assert.True((text.Length == 0) == (other.Length == 0), $"{code} {path}: empty vs filled");
            var want = Placeholder().Matches(text).Select(m => m.Value).Order(StringComparer.Ordinal);
            var got = Placeholder().Matches(other).Select(m => m.Value).Order(StringComparer.Ordinal);
            Assert.True(want.SequenceEqual(got, StringComparer.Ordinal), $"{code} {path}: placeholders differ: {text} | {other}");
        }
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void TranslatedLabelsDifferFromEnglish(string code)
    {
        var reference = PlanReference.For(code);
        Assert.Equal(code, reference.Language);
        Assert.NotEqual("Findings", reference.Ui("tab.findings"));
        Assert.NotEqual(PlanReference.Default.Banners["estimate"], reference.Banners["estimate"]);
        Assert.NotEqual(PlanReference.Default.Rule("PRED-01").Title, reference.Rule("PRED-01").Title);
        Assert.Equal(PlanReference.Default.Rule("PRED-01").Level, reference.Rule("PRED-01").Level);
        Assert.Equal(PlanReference.Default.CourseUrl, reference.CourseUrl);
    }

    [Theory]
    [MemberData(nameof(IdeLanguages))]
    public void TheIdeTranslationFileNameSelectsTheLanguage(string file, string code)
    {
        Assert.Equal(code, PlanReference.LanguageCode(file));
        Assert.Equal(code, PlanReference.LanguageCode(file.Replace(".lang", "", StringComparison.Ordinal)));
        Assert.Equal(code, PlanReference.For("", file).Language);
        Assert.Equal(code, PlanReference.For(file.Replace(".lang", "", StringComparison.Ordinal).ToUpperInvariant()).Language);
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
    public void NativeLanguageNamesAreRecognized(string name, string code) => Assert.Equal(code, PlanReference.LanguageCode(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("English")]
    [InlineData("Klingon")]
    public void NoTranslationOrAnUnknownOneMeansEnglish(string? name)
    {
        Assert.Null(PlanReference.LanguageCode(name));
        Assert.Same(PlanReference.Default, PlanReference.For(name));
        Assert.Equal("", PlanReference.Default.Language);
    }

    [Fact]
    public void ReferenceIsCachedPerLanguageAndMissingKeysShowTheKey()
    {
        Assert.Same(PlanReference.For("Russian"), PlanReference.For("Russian.lang"));
        Assert.Equal("no.such.key", PlanReference.Default.Ui("no.such.key"));
        Assert.Equal("Step 3: reads", PlanReference.Default.Ui("desc.step", ("n", "3"), ("what", "reads")));
    }

    [Fact]
    public void TranslationKeepsEnglishForWhatItLacks()
    {
        var partial = PlanReference.Default.WithTranslation("""{ "ui": { "tab.plan": "Plano", "tab.text": "" }, "rules": { "PRED-01": { "title": "Titulo" } }, "notes": { "SQL profile": { "meaning": "Significado" } } }""", "xx");
        Assert.Equal("Plano", partial.Ui("tab.plan"));
        Assert.Equal("Text", partial.Ui("tab.text")); // empty translation: English stays
        Assert.Equal("Findings", partial.Ui("tab.findings"));
        Assert.Equal("Titulo", partial.Rule("PRED-01").Title);
        Assert.Equal(PlanReference.Default.Rule("PRED-01").Detail, partial.Rule("PRED-01").Detail);
        Assert.Equal("Significado", partial.Note("SQL profile")!.Meaning);
        Assert.Equal(PlanReference.Default.Note("SQL profile")!.Action, partial.Note("SQL profile")!.Action);
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void TheReportHasNoEnglishLabelsInATranslatedLanguage(string code)
    {
        var reference = PlanReference.For(code);
        foreach (var name in new[] { "implicit_conversion", "no_stats", "adaptive", "filter_subquery" })
        {
            var (sql, data) = PlanFixtures.Load(name);
            var html = PlanReport.Html(sql + " and x = :b", PlanAnalysis.Analyze(data, sql, reference), data.Text, null, At, reference);
            foreach (var english in new[] { "Findings (", "Operations in this plan", "Notes of this plan", "Learn more in the course", "Statement:", "Why it may be fine:", "Normal when:", "Suspicious when:", "No indexes.", "no statistics", "Indexes:", "rows, ", "> Copy<", " to check", "Text Tools for PL/SQL Developer, 2026" })
            {
                // The footer keeps the product name; everything else is translated.
                if (english.StartsWith("Text Tools", StringComparison.Ordinal)) continue;
                Assert.DoesNotContain(english, html, StringComparison.Ordinal);
            }
            Assert.Contains(System.Net.WebUtility.HtmlEncode(reference.Ui("tab.findings")), html, StringComparison.Ordinal);
            Assert.Contains(System.Net.WebUtility.HtmlEncode(reference.Ui("stmt.label")), html, StringComparison.Ordinal);
            Assert.DoesNotContain("class=\"error\"", html, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void TranslatedStepTextsAndAnnotationsAreUsed(string code)
    {
        var reference = PlanReference.For(code);
        var (sql, data) = PlanFixtures.Load("nl_full_inner");
        var a = PlanAnalysis.Analyze(data, sql, reference);
        var inner = a.Tree.Step(4)!;
        Assert.Equal(reference.Ui("ann.nestedLoops.many", ("step", "2"), ("n", "50")), inner.Annotations[0]);
        Assert.DoesNotContain("Step ", a.Describe(inner), StringComparison.Ordinal);
        Assert.Contains(reference.Ui("desc.perExecution"), a.Describe(inner), StringComparison.Ordinal);
    }

    [Fact]
    public void TheStatementIsAboveThePlanTable()
    {
        var (sql, data) = PlanFixtures.Load("implicit_conversion");
        var html = PlanReport.Html("select unique_marker from dual", PlanAnalysis.Analyze(data, sql), data.Text, null, At);
        var statement = html.IndexOf("unique_marker", StringComparison.Ordinal);
        var table = html.IndexOf("<table class=\"grid\">", StringComparison.Ordinal);
        Assert.InRange(statement, 1, table - 1);
        Assert.Equal(html.LastIndexOf("unique_marker", StringComparison.Ordinal), statement); // shown once
    }

    [Fact]
    public void TheCopyButtonMessagesAreTranslated()
    {
        var reference = PlanReference.For("Russian");
        var (sql, data) = PlanFixtures.Load("no_stats");
        var html = PlanReport.Html(sql, PlanAnalysis.Analyze(data, sql, reference), data.Text, null, At, reference);
        Assert.Contains($"data-ok=\"{reference.Ui("text.copied")}\"", html, StringComparison.Ordinal);
        Assert.Contains("data-select=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("msg('Copied')", html, StringComparison.Ordinal);
    }
}
