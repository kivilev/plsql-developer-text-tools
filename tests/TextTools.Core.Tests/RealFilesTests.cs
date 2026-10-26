using System.Text;
using TextTools.Core.Formatting;

namespace TextTools.Core.Tests;

/// <summary>
/// Package bodies of a real code base, made anonymous (names, texts and comments replaced, every line keeps its length) and the
/// result of the reference formatter for each. The post-pass must agree with the reference, never change a token, settle, and
/// survive every setting being switched off on its own. (file04: the reference leaves a lower-case <c>end;</c> without the name, the
/// plug-in names it too, so that expected file is the plug-in result, checked by hand. file08: the reference splits <c>SUBSTR(SQLERRM, 1, 200)</c>
/// into one argument per line after a long <c>||</c> chain, against section 5 of the code standard; the plug-in breaks at the operator instead. file11: a line that only its closing semicolon pushes past the margin stays on one line, as the reference now does. file04, file05, file06: a line that starts inside an open parenthesis (an argument of a multi-line call) is cut only past 120 columns, as the reference now does.)
/// </summary>
public class RealFilesTests
{
    public static TheoryData<string> Files
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Real"), "*.input.txt").Order(StringComparer.Ordinal))
            {
                data.Add(Path.GetFileName(file)["file".Length..^".input.txt".Length]);
            }
            return data;
        }
    }

    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Real", name), Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static IEnumerable<string> NonBlankLines(string text) => text.Split('\n').Where(l => l.Trim().Length > 0);

    [Theory]
    [MemberData(nameof(Files))]
    public void TheFileGetsTheLayoutOfTheReferenceWithoutChangingATokenAndSettles(string id)
    {
        var input = Read($"file{id}.input.txt");
        // The reference formatter leaves the commas as written; the blank after a comma is our addition on top of it.
        Assert.True(PostFormatter.TryFormat(input, FormatOptions.Default with { SpaceAfterComma = false }, out var once), "the token guard objected");
        Assert.Equal(NonBlankLines(Read($"file{id}.expected.txt")), NonBlankLines(once));
        Assert.True(PostFormatter.TryFormat(input, null, out var withBlanks), "the token guard objected (blank after comma)");
        Assert.Equal(NonBlankLines(once).Count(), NonBlankLines(withBlanks).Count());
        var settled = once;
        for (var pass = 0; pass < 2 && !string.Equals(PostFormatter.Format(settled), settled, StringComparison.Ordinal); pass++) settled = PostFormatter.Format(settled);
        Assert.Equal(settled, PostFormatter.Format(settled));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EverySettingSwitchedOffAloneStillGivesASafeResult(string id)
    {
        var input = Read($"file{id}.input.txt");
        foreach (var setting in FormatSettings.All.Where(s => s.Kind == SettingKind.Bool))
        {
            var profile = new FormatProfile();
            profile.Set(setting.Id, 1 - setting.Default);
            Assert.True(PostFormatter.TryFormat(input, profile.ToOptions(), out var result), setting.Id);
            Assert.True(TokenGuard.Same(input, result), setting.Id);
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void NothingSwitchedOnLeavesTheFileAsItIs(string id)
    {
        var input = Read($"file{id}.input.txt");
        Assert.Equal(input, PostFormatter.Format(input, FormatOptions.None));
    }
}
