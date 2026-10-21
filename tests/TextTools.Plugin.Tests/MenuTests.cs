using TextTools.Core;

namespace TextTools.Plugin.Tests;

public class MenuTests
{
    private static List<(int Index, string Caption)> Build(int sysVersion)
    {
        Menu.Build(sysVersion);
        return Enumerable.Range(1, 99).Select(i => (i, Menu.Caption(i))).Where(x => x.Item2.Length > 0).ToList();
    }

    [Fact]
    public void RibbonLayoutHasOneGroupAndASubmenuPerMenu()
    {
        var captions = Build(1600).Select(x => x.Caption).ToList();

        Assert.Equal($"GROUP={Menu.Title}", captions[0]);
        Assert.Equal(["MENUITEM=String Tools", "MENUITEM=Date Tools", "MENUITEM=SQL Tools"], captions.Where(c => c.StartsWith("MENUITEM=", StringComparison.Ordinal)));
        Assert.Equal(Commands.All.Count, captions.Count(c => c.StartsWith("SUBITEM=", StringComparison.Ordinal) && !string.Equals(c, "SUBITEM=-", StringComparison.Ordinal)));
    }

    [Fact]
    public void SeparatorsOnlyBetweenGroupsInsideAMenu()
    {
        var captions = Build(1600).Select(x => x.Caption).ToList();
        for (var i = 0; i < captions.Count; i++)
        {
            if (!string.Equals(captions[i], "SUBITEM=-", StringComparison.Ordinal)) continue;
            Assert.StartsWith("SUBITEM=", captions[i - 1], StringComparison.Ordinal);
            Assert.NotEqual("SUBITEM=-", captions[i - 1]);
            Assert.StartsWith("SUBITEM=", captions[i + 1], StringComparison.Ordinal);
        }
        Assert.NotEqual("SUBITEM=-", captions[^1]);
    }

    [Fact]
    public void EveryCommandIsReachableByItsIndex()
    {
        var found = Build(1600).Select(x => Menu.Find(x.Index)).OfType<Command>().ToList();
        Assert.Equal(Commands.All, found);
    }

    [Fact]
    public void SeparatorsAndHeadersAreNotClickable()
    {
        foreach (var (index, caption) in Build(1600))
        {
            if (caption.StartsWith("SUBITEM=", StringComparison.Ordinal) && !string.Equals(caption, "SUBITEM=-", StringComparison.Ordinal)) continue;
            Assert.Null(Menu.Find(index));
        }
    }

    [Fact]
    public void LegacyLayoutUsesToolsPaths()
    {
        var captions = Build(1100).Select(x => x.Caption).ToList();
        Assert.All(captions, c => Assert.StartsWith($"Tools / {Menu.Title} / ", c, StringComparison.Ordinal));
        Assert.Contains($"Tools / {Menu.Title} / Date Tools / ISO duration -> INTERVAL", captions);
    }

    public static TheoryData<string> Languages => [.. TextTools.Core.Plan.PlanReference.LanguageCodes];

    [Theory]
    [MemberData(nameof(Languages))]
    public void TranslatedMenuHasTheSameShapeAndIndicesAsTheEnglishOne(string code)
    {
        var english = Build(1600);
        Menu.Build(1600, TextTools.Core.Plan.PlanReference.For(code));
        var translated = Enumerable.Range(1, 99).Select(i => (i, Menu.Caption(i))).Where(x => x.Item2.Length > 0).ToList();

        Assert.Equal(english.Select(x => x.Index), translated.Select(x => x.i));
        Assert.Equal(Commands.All, translated.Select(x => Menu.Find(x.i)).OfType<Command>());
        foreach (var ((_, en), (index, tr)) in english.Zip(translated))
        {
            Assert.Equal(en.Split('=')[0], tr.Split('=')[0]); // GROUP= / MENUITEM= / SUBITEM= keep their kind
            Assert.DoesNotContain('[', tr);
            Assert.DoesNotContain(']', tr);
            Assert.DoesNotContain('&', tr);
            Assert.False(Menu.DisplayName(index).StartsWith(' ') || Menu.DisplayName(index).EndsWith(' '));
        }
        var items = translated.Select(x => x.Item2).Where(c => c.StartsWith("SUBITEM=", StringComparison.Ordinal) && !string.Equals(c, "SUBITEM=-", StringComparison.Ordinal)).ToList();
        Assert.Equal(items.Count, items.Distinct(StringComparer.Ordinal).Count()); // two items must not share a caption
        Assert.NotEqual(english.Select(x => x.Caption), translated.Select(x => x.Item2));
    }

    [Fact]
    public void ExplainPlanItemsAreInOneGroupWithCopySql()
    {
        var captions = Build(1600).Select(x => x.Caption).ToList();
        var copy = captions.IndexOf("SUBITEM=Copy SQL with constants resolved");
        Assert.Equal(["SUBITEM=Copy SQL with constants resolved", "SUBITEM=Explain Plan with constants", "SUBITEM=Extended Explain Plan"], captions.Skip(copy).Take(3));
    }

    [Fact]
    public void WithoutTranslationOrUtf8TheMenuIsEnglish()
    {
        Menu.Build(1600);
        Assert.Contains("MENUITEM=String Tools", Enumerable.Range(1, 99).Select(Menu.Caption));
        Assert.Equal("Extended Explain Plan", Menu.DisplayName(Menu.IndexOf("Extended Explain Plan")!.Value));
    }

    [Fact]
    public void OutOfRangeIndicesAreHarmless()
    {
        Build(1600);
        Assert.Equal("", Menu.Caption(0));
        Assert.Equal("", Menu.Caption(100));
        Assert.Equal("", Menu.Caption(-1));
        Assert.Null(Menu.Find(int.MaxValue));
    }
}
