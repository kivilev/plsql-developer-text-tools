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
        Assert.Equal(["MENUITEM=String Tools", "MENUITEM=Date Tools"], captions.Where(c => c.StartsWith("MENUITEM=", StringComparison.Ordinal)));
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
