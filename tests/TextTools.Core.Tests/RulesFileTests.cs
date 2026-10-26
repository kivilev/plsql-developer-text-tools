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

    public static TheoryData<string> EverySettingId()
    {
        var data = new TheoryData<string>();
        foreach (var s in FormatSettings.All) data.Add(s.Id);
        return data;
    }

    public static TheoryData<string> EverySettingWithAnIdeName()
    {
        var data = new TheoryData<string>();
        foreach (var s in FormatSettings.All.Where(s => s.Sources is { } src && src.ContainsKey("PLSQLDev"))) data.Add(s.Id);
        return data;
    }

    public static TheoryData<string> EveryNumberId()
    {
        var data = new TheoryData<string>();
        foreach (var s in FormatSettings.All.Where(s => s.Kind == SettingKind.Number)) data.Add(s.Id);
        return data;
    }

    /// <summary>A value that differs from the default and is inside the range.</summary>
    private static int Other(FormatSetting s) => s.Default + 1 <= s.Max ? s.Default + 1 : s.Default - 1;

    private static string KeyOf(FormatSetting s) => char.ToUpperInvariant(s.Id[0]) + s.Id[1..];

    private static string Text(FormatSetting s, int value)
    {
        if (s.Kind != SettingKind.Bool) return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return value != 0 ? "TRUE" : "FALSE";
    }

    /// <summary>Everything in the catalog really is saved and read: ids are unique and the other value is a legal one.</summary>
    [Fact]
    public void TheCatalogCanBeTestedSettingBySetting()
    {
        Assert.Equal(FormatSettings.All.Count, FormatSettings.All.Select(s => s.Id.ToUpperInvariant()).Distinct(StringComparer.Ordinal).Count());
        foreach (var s in FormatSettings.All)
        {
            Assert.NotEqual(s.Default, Other(s));
            Assert.InRange(Other(s), s.Min, s.Max);
        }
    }

    [Theory]
    [MemberData(nameof(EverySettingId))]
    public void ExportWritesTheSettingWithItsKeyAndValue(string id)
    {
        var s = FormatSettings.Find(id)!;
        var profile = new FormatProfile();
        profile.Set(id, Other(s));
        var lines = profile.ToRulesText().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines, l => l.StartsWith(KeyOf(s) + "=", StringComparison.Ordinal));
        Assert.Contains(KeyOf(s) + "=" + Text(s, Other(s)), lines);
        Assert.Equal(FormatSettings.All.Count + 1, lines.Length); // Version + every setting, once
    }

    [Theory]
    [MemberData(nameof(EverySettingId))]
    public void ImportReadsTheSettingAndTouchesNothingElse(string id)
    {
        var s = FormatSettings.Find(id)!;
        var profile = FormatProfile.FromRulesText("Version=1\r\n" + KeyOf(s) + "=" + Text(s, Other(s)) + "\r\n", out var applied);
        Assert.Equal(1, applied);
        foreach (var other in FormatSettings.All)
        {
            var expected = string.Equals(other.Id, id, StringComparison.Ordinal) ? Other(s) : other.Default;
            Assert.Equal(expected, profile.Get(other.Id));
        }
    }

    [Theory]
    [MemberData(nameof(EverySettingId))]
    public void EachSettingSurvivesTheRulesFileAlone(string id)
    {
        var s = FormatSettings.Find(id)!;
        var profile = new FormatProfile();
        profile.Set(id, Other(s));
        var back = FormatProfile.FromRulesText(profile.ToRulesText(), out var applied);
        Assert.Equal(FormatSettings.All.Count, applied);
        foreach (var other in FormatSettings.All) Assert.Equal(profile.Get(other.Id), back.Get(other.Id));
    }

    [Fact]
    public void EverySettingSurvivesTheRulesFileTogether()
    {
        var profile = new FormatProfile();
        foreach (var s in FormatSettings.All) profile.Set(s.Id, Other(s));
        var back = FormatProfile.FromRulesText(profile.ToRulesText(), out var applied);
        Assert.Equal(FormatSettings.All.Count, applied);
        foreach (var s in FormatSettings.All) Assert.Equal(Other(s), back.Get(s.Id));
        Assert.Equal(profile.ToOptions(), back.ToOptions());
    }

    [Fact]
    public void ARulesFileOfTheDefaultsIsTheDefaultProfile()
    {
        var back = FormatProfile.FromRulesText(new FormatProfile().ToRulesText(), out _);
        foreach (var s in FormatSettings.All) Assert.Equal(s.Default, back.Get(s.Id));
    }

    [Theory]
    [MemberData(nameof(EverySettingId))]
    public void EachSettingSurvivesTheJsonStoreAlone(string id)
    {
        var s = FormatSettings.Find(id)!;
        var profile = new FormatProfile();
        profile.Set(id, Other(s));
        var back = FormatProfile.FromJson(profile.ToJson());
        foreach (var other in FormatSettings.All) Assert.Equal(profile.Get(other.Id), back.Get(other.Id));
    }

    [Theory]
    [MemberData(nameof(EverySettingWithAnIdeName))]
    public void ImportKnowsTheNameThatPlsqlDeveloperGivesTheSetting(string id)
    {
        var s = FormatSettings.Find(id)!;
        var name = s.Sources!["PLSQLDev"];
        var profile = FormatProfile.FromRulesText("Version=1\n" + name + "=" + Text(s, Other(s)) + "\n", out var applied);
        Assert.Equal(1, applied);
        Assert.Equal(Other(s), profile.Get(id));
    }

    [Theory]
    [MemberData(nameof(EverySettingId))]
    public void ImportIgnoresCaseOfKeyAndValueAndTheLineEnding(string id)
    {
        var s = FormatSettings.Find(id)!;
        var text = "﻿Version=1\n  " + KeyOf(s).ToUpperInvariant() + " = " + Text(s, Other(s)).ToLowerInvariant() + " \n";
        var profile = FormatProfile.FromRulesText(text, out var applied);
        Assert.Equal(1, applied);
        Assert.Equal(Other(s), profile.Get(id));
    }

    [Theory]
    [MemberData(nameof(EveryNumberId))]
    public void ImportBringsANumberIntoItsRange(string id)
    {
        var s = FormatSettings.Find(id)!;
        Assert.Equal(s.Max, FormatProfile.FromRulesText(KeyOf(s) + "=" + (s.Max + 1000), out _).Get(id));
        Assert.Equal(s.Min, FormatProfile.FromRulesText(KeyOf(s) + "=0", out _).Get(id));
        Assert.Equal(s.Default, FormatProfile.FromRulesText(KeyOf(s) + "=-5", out _).Get(id)); // not a number we accept: stays the default
    }

    [Fact]
    public void ImportOverwritesOnlyWhatTheFileSaysInAnExistingProfile()
    {
        var profile = new FormatProfile();
        foreach (var s in FormatSettings.All) profile.Set(s.Id, Other(s));
        var count = profile.ReadRulesText("Margin=" + 100 + "\nEndWithName=" + (profile.IsOn("endWithName") ? "FALSE" : "TRUE"));
        Assert.Equal(2, count);
        foreach (var s in FormatSettings.All.Where(s => s.Id is not ("margin" or "endWithName"))) Assert.Equal(Other(s), profile.Get(s.Id));
        Assert.Equal(100, profile.Get("margin"));
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
