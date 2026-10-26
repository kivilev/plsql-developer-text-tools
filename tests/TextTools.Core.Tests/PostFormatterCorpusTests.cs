using System.Text;
using TextTools.Core.Formatting;

namespace TextTools.Core.Tests;

/// <summary>
/// Runs the post-pass over a folder of real files when TEXTTOOLS_CORPUS is set (the code of a team is not in this repository):
/// every file must format without the token guard objecting, and twice give the same text. When TEXTTOOLS_CORPUS_EXPECTED
/// names a folder with the output of a reference implementation, the results must be equal to its files.
/// </summary>
public class PostFormatterCorpusTests
{
    [Fact]
    public void RealFilesFormatSafelyAndIdempotentlyAndLikeTheReference()
    {
        var dir = Environment.GetEnvironmentVariable("TEXTTOOLS_CORPUS");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

        var expectedDir = Environment.GetEnvironmentVariable("TEXTTOOLS_CORPUS_EXPECTED") ?? "";
        var problems = new List<string>();
        var files = Directory.GetFiles(dir);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file, Encoding.UTF8);
            if (!PostFormatter.TryFormat(text, null, out var once)) problems.Add($"{Path.GetFileName(file)}: the token guard objected");
            else if (!string.Equals(PostFormatter.Format(once), once, StringComparison.Ordinal)) problems.Add($"{Path.GetFileName(file)}: not idempotent");
            var reference = Path.Combine(expectedDir, Path.GetFileName(file));
            if (File.Exists(reference) && !string.Equals(File.ReadAllText(reference, Encoding.UTF8), once, StringComparison.Ordinal))
            {
                problems.Add($"{Path.GetFileName(file)}: differs from the reference");
            }
        }
        Assert.True(problems.Count == 0, $"{problems.Count} of {files.Length}: " + string.Join("; ", problems.Take(30)));
    }
}
