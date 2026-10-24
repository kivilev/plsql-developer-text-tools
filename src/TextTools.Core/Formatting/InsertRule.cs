using System.Text;
using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>
/// R13: <c>INSERT INTO t</c>, the column list in parentheses on its own line (indent 2), <c>VALUES</c> on its own line, the value
/// list in parentheses (indent 2). A list that fits the margin stays on one line; a longer one is packed to the margin, the
/// continuation lines under the first item (the parenthesis stays on the first item's line).
/// </summary>
internal static partial class InsertRule
{
    private const string Ident = @"[A-Za-z_][\w$#]*";

    [GeneratedRegex(@"^(?<indent>[ \t]*)INSERT\s+INTO\s+(?<table>" + Ident + @"(?:\s*\.\s*" + Ident + @")?)\s*\(", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Insert();

    [GeneratedRegex(@"\s*VALUES\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Values();

    [GeneratedRegex(@"\b(?:SELECT|CASE)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex SelectOrCase();

    public static string Apply(string text, int margin)
    {
        var masked = LineText.Mask(text);
        var spans = new List<(int Start, int End, string Replacement)>();
        foreach (Match m in Insert().Matches(masked))
        {
            var open = m.Index + m.Length - 1;
            var close = LineText.MatchParen(masked, open);
            if (close < 0) continue;
            var values = Values().Match(masked, close + 1);
            if (!values.Success || values.Index != close + 1) continue;
            var valuesOpen = values.Index + values.Length - 1;
            var valuesClose = LineText.MatchParen(masked, valuesOpen);
            if (valuesClose < 0) continue;
            var region = text[m.Index..(valuesClose + 1)];
            if (LineText.HasComment(region) || SelectOrCase().IsMatch(masked[m.Index..(valuesClose + 1)])) continue;

            var indent = m.Groups["indent"].Value;
            var columns = LineText.ItemsOf(text, masked, open, close);
            var items = LineText.ItemsOf(text, masked, valuesOpen, valuesClose);
            var tail = valuesClose + 1 < masked.Length && masked[valuesClose + 1] == ';' ? ";" : "";
            var replacement = indent + "INSERT INTO " + m.Groups["table"].Value.Trim() + "\n" + FormatList(indent, columns, "", margin) + "\n"
                + indent + "VALUES\n" + FormatList(indent, items, tail, margin).TrimEnd(';');
            spans.Add((m.Index, valuesClose + 1, replacement));
        }
        if (spans.Count == 0) return text;
        var sb = new StringBuilder(text);
        foreach (var (start, end, replacement) in spans.OrderByDescending(s => s.Start))
        {
            sb.Remove(start, end - start).Insert(start, replacement);
        }
        return sb.ToString();
    }

    /// <summary>A list in parentheses: one line if it fits, else packed to the margin with the continuation under the first item.</summary>
    private static string FormatList(string indent, List<string> items, string tail, int margin)
    {
        var one = indent + "  (" + string.Join(", ", items) + ")" + tail;
        if (items.Count == 1 || one.TrimEnd().Length <= margin) return one;
        var first = indent + "  (";
        var rest = indent + "   ";
        var lines = new List<string>();
        var current = new StringBuilder(first);
        var empty = true; // nothing but the opening parenthesis on the line yet
        for (var k = 0; k < items.Count; k++)
        {
            var last = k == items.Count - 1;
            var piece = items[k] + (last ? ")" + tail : ",");
            if (!empty && current.Length + 1 + piece.Length > margin)
            {
                lines.Add(current.ToString());
                current.Clear().Append(rest).Append(piece);
            }
            else
            {
                if (!empty) current.Append(' ');
                current.Append(piece);
            }
            empty = false;
        }
        lines.Add(current.ToString());
        return string.Join('\n', lines);
    }
}
