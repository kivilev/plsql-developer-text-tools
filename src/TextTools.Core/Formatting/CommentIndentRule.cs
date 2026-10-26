using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>A comment on a line of its own takes the indent of the code that follows it (of the code before it when a closing keyword follows).</summary>
internal static partial class CommentIndentRule
{
    [GeneratedRegex(@"^\s*(?:END|EXCEPTION|ELSE|ELSIF|WHEN)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ClosingKeyword();

    public static string Apply(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = (string[])lines.Clone();

        bool IsComment(int i) => lines[i].TrimStart().StartsWith("--", StringComparison.Ordinal) && masked[i].Trim().Length == 0;

        for (var i = 0; i < lines.Length; i++)
        {
            if (!IsComment(i)) continue;
            int? next = null;
            for (var j = i + 1; j < lines.Length; j++)
            {
                if (lines[j].Trim().Length > 0 && !IsComment(j))
                {
                    next = j;
                    break;
                }
            }
            int? previous = null;
            for (var j = i - 1; j >= 0; j--)
            {
                if (lines[j].Trim().Length > 0 && !IsComment(j))
                {
                    previous = j;
                    break;
                }
            }
            if (next is null) continue;
            var indent = ClosingKeyword().IsMatch(masked[next.Value]) && previous is not null ? LineText.Indent(lines[previous.Value]) : LineText.Indent(lines[next.Value]);
            output[i] = new string(' ', indent) + lines[i].TrimStart();
        }
        return string.Join('\n', output);
    }
}
