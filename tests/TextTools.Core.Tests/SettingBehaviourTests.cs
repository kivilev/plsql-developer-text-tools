using TextTools.Core.Formatting;

namespace TextTools.Core.Tests;

/// <summary>
/// The rule "a new setting comes with a test" enforced by the build: these theories run over the whole catalog
/// (<see cref="FormatSettings.All"/>), so a setting that does nothing, or is not wired to its rule, fails here by itself.
/// </summary>
public class SettingBehaviourTests
{
    public static TheoryData<string> BoolIds()
    {
        var data = new TheoryData<string>();
        foreach (var s in FormatSettings.All.Where(s => s.Kind == SettingKind.Bool)) data.Add(s.Id);
        return data;
    }

    private static FormatProfile EverythingOff()
    {
        var profile = new FormatProfile();
        foreach (var s in FormatSettings.All.Where(s => s.Kind == SettingKind.Bool)) profile.Set(s.Id, 0);
        return profile;
    }

    /// <summary>The setting on its own does its work: with every other switch off, turning it on changes the preview sample.</summary>
    [Theory]
    [MemberData(nameof(BoolIds))]
    public void ASwitchWorksOnItsOwn(string id)
    {
        // These only act while "wrap long lines" is on (the settings window greys them out otherwise): it is their precondition.
        var needsWrapping = id is "wrapDecodeInPairs" or "wrapBetween" or "alignNamedArguments";
        var baseProfile = EverythingOff();
        if (needsWrapping) baseProfile.Set("wrapLongLines", 1);
        var allOff = PostFormatter.Format(FormatSample.Text, baseProfile.ToOptions());
        var profile = baseProfile.Clone();
        profile.Set(id, 1);
        var result = PostFormatter.Format(FormatSample.Text, profile.ToOptions());
        // alignAliases is the opposite way round: its "off" is the active rule (one blank before an alias), and "everything off" has it.
        Assert.NotEqual(allOff, result);
    }

    /// <summary>The setting in its non-default position changes the sample against the default profile.</summary>
    [Theory]
    [MemberData(nameof(BoolIds))]
    public void ASwitchChangesTheDefaultResult(string id)
    {
        var s = FormatSettings.Find(id)!;
        var profile = new FormatProfile();
        profile.Set(id, 1 - s.Default);
        Assert.NotEqual(PostFormatter.Format(FormatSample.Text), PostFormatter.Format(FormatSample.Text, profile.ToOptions()));
    }

    /// <summary>
    /// The default profile is the bank's code standard (cbs-abs-db/_docs/coding-standards.md §5-6 and _tools/ide/formatter/cbs_post.py):
    /// every rule of the reference formatter is on, margin 76. Off is only what the standard does not ask for:
    /// a blank line right after the package header ("not needed"). A blank after a comma (<c>SUBSTR(x, 1, 3)</c>) is on, as in its examples.
    /// </summary>
    [Fact]
    public void TheDefaultProfileIsTheBankStandard()
    {
        var off = FormatSettings.All.Where(s => s.Kind == SettingKind.Bool && s.Default == 0).Select(s => s.Id).Order(StringComparer.Ordinal);
        Assert.Equal(["blankLineAfterPackageHeader"], off);
        Assert.Equal(76, FormatSettings.Find("margin")!.Default);
    }

    [Fact]
    public void TheMarginMovesTheWrapping()
    {
        var narrow = new FormatProfile();
        narrow.Set("margin", 40);
        var wide = new FormatProfile();
        wide.Set("margin", 200);
        Assert.NotEqual(
            PostFormatter.Format(FormatSample.Text, narrow.ToOptions()),
            PostFormatter.Format(FormatSample.Text, wide.ToOptions()));
    }

    /// <summary>A number setting that is not covered by a test of its own is a gap in this file: add its test and its id here.</summary>
    [Fact]
    public void EveryNumberSettingHasATestOfItsOwn()
    {
        var withTest = new HashSet<string>(StringComparer.Ordinal) { "margin" }; // TheMarginMovesTheWrapping
        var missing = FormatSettings.All.Where(s => s.Kind == SettingKind.Number && !withTest.Contains(s.Id)).Select(s => s.Id).ToList();
        Assert.True(missing.Count == 0, "Add a unit test for the setting(s): " + string.Join(", ", missing));
    }
}
