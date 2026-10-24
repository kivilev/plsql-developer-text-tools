using System.Text;
using TextTools.Core.Formatting;

namespace TextTools.Core.Tests;

/// <summary>
/// Runs the post-pass over a folder of real files when TEXTTOOLS_CORPUS is set (the code of a team is not in this repository):
/// every file must format without the token guard objecting and settle within three passes (one file of the ICBS corpus needs a
/// second pass: an assignment group that the wrap rule later splits is aligned again). When TEXTTOOLS_CORPUS_EXPECTED names a
/// folder with the output of the reference implementation (cbs_post.py), the results must be equal to its files except for blank
/// lines: the reference makes an extra blank line of a line that ends with the concatenation operator, this port does not.
/// TEXTTOOLS_CORPUS_OUT receives the results.
/// </summary>
public class PostFormatterCorpusTests
{
    [Fact]
    public void RealFilesFormatSafelyAndSettleAndMatchTheReference()
    {
        var dir = Environment.GetEnvironmentVariable("TEXTTOOLS_CORPUS");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

        var expectedDir = Environment.GetEnvironmentVariable("TEXTTOOLS_CORPUS_EXPECTED") ?? "";
        var problems = new List<string>();
        var files = Directory.GetFiles(dir);
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var text = File.ReadAllText(file, Encoding.UTF8);
            if (!PostFormatter.TryFormat(text, null, out var once))
            {
                problems.Add($"{name}: the token guard objected");
                continue;
            }
            var settled = once;
            for (var pass = 0; pass < 2 && !string.Equals(PostFormatter.Format(settled), settled, StringComparison.Ordinal); pass++) settled = PostFormatter.Format(settled);
            if (!string.Equals(PostFormatter.Format(settled), settled, StringComparison.Ordinal)) problems.Add($"{name}: does not settle in three passes");

            if (Environment.GetEnvironmentVariable("TEXTTOOLS_CORPUS_OUT") is { Length: > 0 } outDir)
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, name), once, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            var reference = Path.Combine(expectedDir, name);
            if (File.Exists(reference) && !NonBlankLines(File.ReadAllText(reference, Encoding.UTF8)).SequenceEqual(NonBlankLines(once), StringComparer.Ordinal))
            {
                problems.Add($"{name}: differs from the reference");
            }
        }
        Assert.True(problems.Count == 0, $"{problems.Count} of {files.Length}: " + string.Join("; ", problems.Take(30)));
    }

    private static IEnumerable<string> NonBlankLines(string text) => text.Split('\n').Where(l => l.Trim().Length > 0);
}
