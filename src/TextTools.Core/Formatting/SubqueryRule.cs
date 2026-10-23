using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>
/// A subquery in parentheses: <c>(</c> at the end of a line with SELECT on the next goes on one line when it fits and has no comment;
/// otherwise the SELECT follows the parenthesis and the block is shifted (<c>)</c> on its own line when the last line has a comment).
/// </summary>
internal static partial class SubqueryRule
{
    [GeneratedRegex(@"^\s*SELECT\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex SelectLine();

    [GeneratedRegex(@"--|/\*", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex CommentMarker();

    private const int Passes = 8;

    public static string Apply(string text, int margin, int maxColumn = 50)
    {
        for (var pass = 0; pass < Passes; pass++)
        {
            var (lines, masked) = LineText.Split(text);
            var output = new List<string>(lines.Length);
            var changed = false;
            var i = 0;
            while (i < lines.Length)
            {
                if (TryRewrite(lines, masked, i, margin, maxColumn, output, out var next))
                {
                    i = next;
                    changed = true;
                    continue;
                }
                output.Add(lines[i]);
                i++;
            }
            text = string.Join('\n', output);
            if (!changed) break;
        }
        return text;
    }

    private static bool TryRewrite(string[] lines, string[] masked, int i, int margin, int maxColumn, List<string> output, out int next)
    {
        next = i;
        if (!masked[i].TrimEnd().EndsWith('(') || i + 1 >= lines.Length || !SelectLine().IsMatch(masked[i + 1])) return false;
        var balance = 0;
        var j = i;
        while (j < lines.Length)
        {
            balance += LineText.Net(masked[j]);
            if (balance <= 0 && j > i) break;
            j++;
        }
        if (j >= lines.Length || !masked[j].TrimStart().StartsWith(')')) return false;
        // A nested subquery inside: its own turn comes in the next pass.
        for (var x = i + 1; x < j; x++)
        {
            if (x + 1 < lines.Length && masked[x].Contains('(', StringComparison.Ordinal) && masked[x].TrimEnd().EndsWith('(') && SelectLine().IsMatch(masked[x + 1])) return false;
        }
        var body = Enumerable.Range(i + 1, j - i - 1).ToList();
        if (body.Count == 0) return false;
        var hasComment = body.Any(x => LineText.HasComment(lines[x]));
        var column = lines[i].TrimEnd().Length;
        var tail = lines[j].TrimStart()[1..];
        var one = lines[i].TrimEnd() + string.Join(' ', body.Select(x => LineText.Squash(lines[x], masked[x]))) + ")" + tail;
        var codeLength = CommentMarker().IsMatch(one) ? CommentMarker().Split(one)[0].TrimEnd().Length : one.TrimEnd().Length;
        if (codeLength <= margin && !hasComment)
        {
            output.Add(one);
            next = j + 1;
            return true;
        }
        if (column > maxColumn) return false;

        var delta = column - LineText.Indent(lines[body[0]]);
        var shifted = new List<string>();
        for (var k = 0; k < body.Count; k++)
        {
            var line = lines[body[k]];
            if (line.Trim().Length == 0)
            {
                shifted.Add(line);
            }
            else if (k == 0)
            {
                shifted.Add(line.TrimStart());
            }
            else if (delta >= 0)
            {
                shifted.Add(new string(' ', delta) + line);
            }
            else
            {
                shifted.Add(line[Math.Min(-delta, LineText.Indent(line))..]);
            }
        }
        output.Add(lines[i].TrimEnd() + shifted[0]);
        output.AddRange(shifted.Skip(1));
        if (LineText.HasComment(lines[body[^1]])) output.Add(new string(' ', column - 1) + ")" + tail);
        else output[^1] = output[^1].TrimEnd() + ")" + tail;
        next = j + 1;
        return true;
    }
}
