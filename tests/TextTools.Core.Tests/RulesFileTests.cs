using TextTools.Core.Formatting;

namespace TextTools.Core.Tests;

/// <summary>The rules file in the style of PL/SQL Developer's .br file: our own profile, and a real .br of the Beautifier as input.</summary>
public class RulesFileTests
{
    [Fact]
    public void AProfileSurvivesTheRulesFile()
    {
        var profile = new FormatProfile();
        profile.Set("margin", 100);
        profile.Set("endWithName", 0);
        profile.Set("spaceAfterComma", 1);
        var text = profile.ToRulesText();
        Assert.StartsWith("Version=1\r\n", text, StringComparison.Ordinal);
        Assert.Contains("Margin=100\r\n", text, StringComparison.Ordinal);
        Assert.Contains("EndWithName=FALSE\r\n", text, StringComparison.Ordinal);
        var back = FormatProfile.FromRulesText(text, out var applied);
        Assert.Equal(FormatSettings.All.Count, applied);
        Assert.Equal(profile.ToOptions(), back.ToOptions());
    }

    [Fact]
    public void TheRulesFileOfTheBeautifierGivesTheSettingsThatBothHave()
    {
        const string br = "Version=1\nRightMargin=90\nIndent=2\nUseTabCharacter=FALSE\nAlignDeclarationGroups=TRUE\nAlignAssignmentGroups=FALSE\nKeywordCase=1\n";
        var profile = FormatProfile.FromRulesText(br, out var applied);
        Assert.Equal(3, applied); // RightMargin, AlignDeclarationGroups and AlignAssignmentGroups; the rest is the Beautifier's own business
        Assert.Equal(90, profile.Get("margin"));
        Assert.False(profile.IsOn("alignAssignments"));
        Assert.True(profile.IsOn("alignDeclarations"));
        Assert.True(profile.IsOn("endWithName")); // not in the file: the default
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no equals sign here")]
    [InlineData("Margin=abc\nEndWithName=maybe")]
    public void NothingUsefulInTheFileMeansTheDefaultProfile(string? text)
    {
        var profile = FormatProfile.FromRulesText(text, out var applied);
        Assert.Equal(0, applied);
        Assert.Equal(FormatOptions.Default, profile.ToOptions());
    }
}
