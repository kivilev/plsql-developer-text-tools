using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>
/// The fields of <c>TYPE t IS RECORD (</c> and of <c>CREATE TYPE t AS OBJECT (</c>: the parenthesis on the header line after one
/// blank, the fields four columns right of the header (their own alignment kept), the closing parenthesis on a line of its own
/// under the header.
/// </summary>
internal static partial class TypeFieldsRule
{
    [GeneratedRegex(@"\b(?:RECORD|OBJECT)\s*\(\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex HeaderWithParen();

    [GeneratedRegex(@"\b(?:AS|IS)\s+OBJECT\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex HeaderObject();

    [GeneratedRegex(@"^\s*(?:CREATE\b.*\bTYPE|TYPE)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex TypeStart();

    [GeneratedRegex(@"\s*\(\s*$", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex TrailingParen();

    [GeneratedRegex(@"^\s*\(\s*$", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ParenOnly();

    public static string Apply(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = new List<string>(lines.Length + 8);
        var i = 0;
        while (i < lines.Length)
        {
            if (!TypeStart().IsMatch(masked[i]))
            {
                output.Add(lines[i]);
                i++;
                continue;
            }
            var bodyStart = -1;
            string header;
            if (HeaderWithParen().IsMatch(masked[i]))
            {
                header = TrailingParen().Replace(lines[i].TrimEnd(), "", 1) + " (";
                bodyStart = i + 1;
            }
            else if (HeaderObject().IsMatch(masked[i]) && i + 1 < lines.Length && ParenOnly().IsMatch(masked[i + 1]))
            {
                header = lines[i].TrimEnd() + " (";
                bodyStart = i + 2;
            }
            else
            {
                output.Add(lines[i]);
                i++;
                continue;
            }
            // The line with the closing parenthesis of the field list.
            var depth = 1;
            var close = -1;
            var closeAt = -1;
            for (var j = bodyStart; j < lines.Length && close < 0; j++)
            {
                for (var k = 0; k < masked[j].Length; k++)
                {
                    if (masked[j][k] == '(')
                    {
                        depth++;
                    }
                    else if (masked[j][k] == ')' && --depth == 0)
                    {
                        close = j;
                        closeAt = k;
                        break;
                    }
                }
            }
            if (close < 0 || masked[close][(closeAt + 1)..].Trim().TrimStart(';').Trim().Length > 0)
            {
                output.Add(lines[i]);
                i++;
                continue;
            }
            var baseIndent = LineText.Indent(lines[i]);
            var fieldIndent = -1;
            for (var j = bodyStart; j <= close && fieldIndent < 0; j++)
            {
                var part = j == close ? masked[j][..closeAt] : masked[j];
                if (part.Trim().Length > 0) fieldIndent = LineText.Indent(lines[j]);
            }
            if (fieldIndent < 0)
            {
                output.Add(lines[i]);
                i++;
                continue;
            }
            var shift = baseIndent + 4 - fieldIndent;
            output.Add(header);
            for (var j = bodyStart; j < close; j++) output.Add(Shift(lines[j], shift));
            var body = lines[close][..closeAt];
            if (body.Trim().Length > 0) output.Add(Shift(body.TrimEnd(), shift));
            output.Add(new string(' ', baseIndent) + lines[close][closeAt..].Trim());
            i = close + 1;
        }
        return string.Join('\n', output);
    }

    private static string Shift(string line, int shift)
    {
        if (line.Trim().Length == 0) return line;
        var indent = LineText.Indent(line);
        return new string(' ', Math.Max(0, indent + shift)) + line.TrimStart();
    }
}
