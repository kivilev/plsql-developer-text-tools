using System.Text;
using System.Text.RegularExpressions;
using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>
/// <c>JSON_OBJECT('a' VALUE x, 'b' VALUE y)</c> with key / value pairs is laid out like the reference formatter does it: the
/// opening parenthesis ends the line, every pair is on its own line two columns right of the <c>JSON_OBJECT</c>, the closing
/// parenthesis starts a line under the <c>J</c>. Only the outermost call of a nested one is laid out; <c>KEY ... IS</c> pairs and
/// calls without pairs are left as they are.
/// </summary>
internal static partial class JsonObjectRule
{
    [GeneratedRegex(@"\bJSON_OBJECT\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Call();

    [GeneratedRegex(@"\sVALUE\s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ValueKeyword();

    [GeneratedRegex(@"\bSELECT\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Subquery();

    [GeneratedRegex(@"^[ \t]*\n", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex LineEnd();

    [GeneratedRegex(@"^(?<item>.*?)\s+(?<clause>RETURNING\s+\w+(?:\s*\(\s*\d+\s*\))?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline, Limits.RegexTimeoutMs)]
    private static partial Regex TrailingReturning();

    /// <summary>A pass lays out the outermost calls; the calls inside their pairs are laid out by the next pass (nesting is a few levels deep).</summary>
    public static string Apply(string text)
    {
        for (var pass = 0; pass < 6; pass++)
        {
            var next = LayOut(text);
            if (string.Equals(next, text, StringComparison.Ordinal)) break;
            text = next;
        }
        return text;
    }

    private static string LayOut(string text)
    {
        var masked = PlsqlLexer.Mask(text);
        var edits = new List<(int Start, int End, string Replacement)>();
        var skipUntil = 0;
        foreach (Match m in Call().Matches(masked))
        {
            if (m.Index < skipUntil) continue; // inside a call that is laid out already
            var open = m.Index + m.Length - 1;
            var close = Close(masked, open);
            if (close < 0) continue;
            var inner = masked[(open + 1)..close];
            if (!ValueKeyword().IsMatch(inner)) continue;
            if (Subquery().IsMatch(inner)) continue; // a subquery as a value keeps its own layout
            if (LineEnd().IsMatch(masked[(open + 1)..])) continue; // the parenthesis already ends the line: laid out
            var cuts = LineText.SplitTop(masked, open + 1, close);
            var items = new List<string>();
            var previous = open + 1;
            foreach (var c in cuts)
            {
                items.Add(Squash(text, masked, previous, c));
                previous = c + 1;
            }
            items.Add(Squash(text, masked, previous, close));
            if (items.Any(i => i.Length == 0 || LineText.HasComment(i))) continue;
            if (items.Any(i => i.StartsWith("KEY ", StringComparison.OrdinalIgnoreCase) || i.StartsWith("RETURNING", StringComparison.OrdinalIgnoreCase))) continue;

            string? returning = null; // RETURNING CLOB of the call itself goes on a line of its own, right before the parenthesis
            var returningMatch = TrailingReturning().Match(items[^1]);
            if (returningMatch.Success)
            {
                returning = returningMatch.Groups["clause"].Value;
                items[^1] = returningMatch.Groups["item"].Value;
            }
            var lineStart = text.LastIndexOf('\n', m.Index) + 1;
            var column = m.Index - lineStart;
            // The call must be the first thing of the layout on its line: whatever stands before it is on that line already.
            var sb = new StringBuilder();
            sb.Append(text, m.Index, open + 1 - m.Index);
            for (var k = 0; k < items.Count; k++)
            {
                sb.Append('\n').Append(' ', column + 2).Append(items[k]);
                if (k < items.Count - 1) sb.Append(',');
            }
            sb.Append('\n').Append(' ', column);
            if (returning is not null) sb.Append(returning);
            edits.Add((m.Index, open + 1 + (close - open - 1) + 0, sb.ToString()));
            skipUntil = close;
        }
        if (edits.Count == 0) return text;
        var result = new StringBuilder(text.Length + 64);
        var position = 0;
        foreach (var (start, end, replacement) in edits)
        {
            result.Append(text, position, start - position).Append(replacement);
            position = end;
        }
        result.Append(text, position, text.Length - position);
        return result.ToString();
    }

    private static string Squash(string text, string masked, int start, int end) =>
        LineText.Squash(text[start..end], masked[start..end]);

    private static int Close(string masked, int open)
    {
        var depth = 0;
        for (var i = open; i < masked.Length; i++)
        {
            if (masked[i] == '(') depth++;
            else if (masked[i] == ')' && --depth == 0) return i;
        }
        return -1;
    }
}
