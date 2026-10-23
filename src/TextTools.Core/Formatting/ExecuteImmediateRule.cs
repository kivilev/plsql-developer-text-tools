using System.Text.RegularExpressions;
using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>R21: <c>EXECUTE IMMEDIATE</c> with a literal longer than the margin or running over several lines: the literal starts on its own line, 2 columns deeper.</summary>
internal static partial class ExecuteImmediateRule
{
    [GeneratedRegex(@"^(?<indent>\s*)EXECUTE\s+IMMEDIATE\s+(?<literal>q?'.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Statement();

    [GeneratedRegex(@"^\s*EXECUTE\s+IMMEDIATE\s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex CodeStart();

    public static string Apply(string text, int margin)
    {
        var lines = text.Split('\n');
        var masked = PlsqlLexer.Mask(text).Split('\n');
        var output = new List<string>(lines.Length + 8);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var m = Statement().Match(line);
            // The statement must be code (not the middle of a comment or of a literal) and a comment must not come before the literal.
            if (m.Success && CodeStart().IsMatch(masked[i]) && !BeforeFirstQuote(line).Contains("--", StringComparison.Ordinal)
                && (line.TrimEnd().Length > margin || line.Count(c => c == '\'') % 2 == 1))
            {
                output.Add(m.Groups["indent"].Value + "EXECUTE IMMEDIATE");
                output.Add(m.Groups["indent"].Value + "  " + m.Groups["literal"].Value.TrimEnd());
            }
            else
            {
                output.Add(line);
            }
        }
        return string.Join('\n', output);
    }

    private static string BeforeFirstQuote(string line)
    {
        var q = line.IndexOf('\'', StringComparison.Ordinal);
        return q < 0 ? line : line[..q];
    }
}
