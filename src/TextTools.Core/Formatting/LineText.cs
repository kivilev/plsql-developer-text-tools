using System.Text;
using System.Text.RegularExpressions;
using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>
/// What the line and layout rules share: the text split into lines next to the same lines with comments and literals masked
/// (<see cref="PlsqlLexer.Mask"/>), and the small helpers they use to cut a line into the items of a list.
/// </summary>
internal static partial class LineText
{
    /// <summary>The text and its masked twin, line by line (same number of lines, same lengths).</summary>
    public static (string[] Lines, string[] Masked) Split(string text) => (text.Split('\n'), PlsqlLexer.Mask(text).Split('\n'));

    public static string Mask(string text) => PlsqlLexer.Mask(text);

    public static int Indent(string line) => line.Length - line.TrimStart().Length;

    /// <summary>Opening minus closing parentheses of a masked line.</summary>
    public static int Net(string masked) => masked.Count(c => c == '(') - masked.Count(c => c == ')');

    /// <summary>Does a line carry a comment (the text of a rule that must not move comments).</summary>
    public static bool HasComment(string line) => line.Contains("--", StringComparison.Ordinal) || line.Contains("/*", StringComparison.Ordinal);

    [GeneratedRegex(@"^\s*(?:--|/\*)", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex CommentOnlyLine();

    public static bool IsCommentOnly(string line) => CommentOnlyLine().IsMatch(line);

    /// <summary>The positions of the commas at parentheses depth 0 in <c>masked[start..end)</c>.</summary>
    public static List<int> SplitTop(string masked, int start, int end)
    {
        var depth = 0;
        var cuts = new List<int>();
        for (var i = start; i < end; i++)
        {
            switch (masked[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    cuts.Add(i);
                    break;
            }
        }
        return cuts;
    }

    /// <summary>The index of the parenthesis that closes the one at <paramref name="open"/>, or -1.</summary>
    public static int MatchParen(string masked, int open)
    {
        var depth = 0;
        for (var k = open; k < masked.Length; k++)
        {
            if (masked[k] == '(')
            {
                depth++;
            }
            else if (masked[k] == ')')
            {
                depth--;
                if (depth == 0) return k;
            }
        }
        return -1;
    }

    /// <summary>Whitespace runs outside strings and comments collapsed to one blank, the result trimmed.</summary>
    public static string Squash(string text, string masked)
    {
        var sb = new StringBuilder(text.Length);
        var previousBlank = false;
        for (var i = 0; i < text.Length; i++)
        {
            var isBlank = char.IsWhiteSpace(masked[i]);
            if (isBlank)
            {
                if (!previousBlank) sb.Append(' ');
            }
            else
            {
                sb.Append(text[i]);
            }
            previousBlank = isBlank;
        }
        return sb.ToString().Trim();
    }

    /// <summary>The items of the list in the parentheses at <c>[open, close]</c>, each squashed.</summary>
    public static List<string> ItemsOf(string text, string masked, int open, int close)
    {
        var cuts = SplitTop(masked, open + 1, close);
        var bounds = new List<int> { open + 1 };
        bounds.AddRange(cuts.Select(c => c + 1));
        bounds.Add(close + 1);
        var items = new List<string>();
        for (var k = 0; k < bounds.Count - 1; k++)
        {
            int s = bounds[k], e = bounds[k + 1] - 1;
            items.Add(Squash(text[s..e], masked[s..e]));
        }
        return items;
    }
}
