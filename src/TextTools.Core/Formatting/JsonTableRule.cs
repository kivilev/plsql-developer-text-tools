using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>
/// JSON_TABLE: <c>FROM JSON_TABLE(expr, 'path'</c> on one line, <c>COLUMNS (</c> under the first argument, one column per line,
/// <c>NESTED PATH ... COLUMNS (...)</c> as a block two columns right of its entries, then the closing parentheses and the alias:
/// <code>
///   FROM JSON_TABLE(iJson, '$[*]'
///                   COLUMNS (a NUMBER PATH '$.a',
///                            b VARCHAR2(5) PATH '$.b')) jt
/// </code>
/// A JSON_TABLE with a comment, an ON ERROR clause after COLUMNS or an entry it does not know is left alone.
/// </summary>
internal static partial class JsonTableRule
{
    private const int Hard = 120;
    private const int MaxPasses = 500;

    [GeneratedRegex(@"\bJSON_TABLE\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex JsonTable();

    [GeneratedRegex(@"\bCOLUMNS\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Columns();

    [GeneratedRegex(@"\bCOLUMNS\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ColumnsWord();

    [GeneratedRegex(@"^\s*NESTED\b.*?\bCOLUMNS\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline, Limits.RegexTimeoutMs)]
    private static partial Regex Nested();

    [GeneratedRegex(@"\s*COLUMNS$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ColumnsTail();

    [GeneratedRegex(@"^\s*(?<alias>[A-Za-z_][\w$#]*)(?=\s*(?:$|[,;)\n]|\s(?:WHERE|ON|JOIN|CROSS|OUTER|LEFT|ORDER|GROUP)\b))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Alias();

    [GeneratedRegex(@"(?:\bFROM|\bJOIN|\bAPPLY|,)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex SourceBefore();

    public static string Apply(string text)
    {
        var pos = 0;
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var masked = LineText.Mask(text);
            var m = JsonTable().Match(masked, pos);
            if (!m.Success) return text;
            pos = m.Index + m.Length;
            var close = LineText.MatchParen(masked, m.Index + m.Length - 1);
            if (close < 0) continue;
            var cm = Columns().Match(masked[pos..close]);
            if (!cm.Success || LineText.HasComment(text[m.Index..close])) continue;
            var cOpen = pos + cm.Index + cm.Length - 1;
            var cClose = LineText.MatchParen(masked, cOpen);
            if (cClose < 0 || masked[(cClose + 1)..close].Trim().Length > 0 || Entries(text, masked, cOpen, 0) is null) continue;
            var head = LineText.Squash(text[pos..(pos + cm.Index)], masked[pos..(pos + cm.Index)]);
            var alias = Alias().Match(masked[(close + 1)..]);
            var tailEnd = close + 1 + (alias.Success ? alias.Length : 0);
            // FROM / JSON_TABLE on the next line: back on the FROM line.
            var start = m.Index;
            var lineStart = masked.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
            if (start == 0) lineStart = 0;
            string lead;
            if (masked[lineStart..start].Trim().Length == 0 && lineStart > 0)
            {
                var previous = masked[..(lineStart - 1)].TrimEnd();
                if (SourceBefore().IsMatch(previous))
                {
                    start = previous.Length;
                    lead = text[..start] + " ";
                }
                else
                {
                    lead = text[..start];
                }
            }
            else
            {
                lead = text[..start];
            }
            var lineLead = lead[(lead.LastIndexOf('\n') + 1)..];
            var column = lineLead.Length + "JSON_TABLE(".Length;
            var columns = Entries(text, masked, cOpen, column)!;
            var width = Math.Max(column + "COLUMNS (".Length + columns[0].Length, columns.Skip(1).Select(x => x.Length).DefaultIfEmpty(0).Max()) + 2;
            if (width > Hard)
            {
                column = LineText.Indent(lineLead) + 2;
                columns = Entries(text, masked, cOpen, column)!;
            }
            var name = text[m.Index..(m.Index + m.Length - 1)].Trim();
            var bodyLines = new List<string> { name + "(" + head, new string(' ', column) + "COLUMNS (" + columns[0] };
            bodyLines.AddRange(columns.Skip(1));
            var body = string.Join('\n', bodyLines) + ")" + (alias.Success ? " " + alias.Groups["alias"].Value : "");
            var replaced = lead + body + text[tailEnd..];
            text = replaced;
            pos = lead.Length + body.Length;
        }
        return text;
    }

    /// <summary>
    /// The lines of <c>COLUMNS (...)</c> at <paramref name="open"/> when <c>COLUMNS (</c> starts at <paramref name="column"/>: the
    /// first line goes right after <c>COLUMNS (</c>, the others carry their indent; the last ends with the closing parenthesis.
    /// Null for an entry this rule does not know.
    /// </summary>
    private static List<string>? Entries(string text, string masked, int open, int column)
    {
        var close = LineText.MatchParen(masked, open);
        if (close < 0) return null;
        var cuts = LineText.SplitTop(masked, open + 1, close);
        var bounds = new List<int> { open + 1 };
        bounds.AddRange(cuts.Select(c => c + 1));
        bounds.Add(close + 1);
        var entryColumn = column + "COLUMNS (".Length;
        var lines = new List<string>();
        for (var k = 0; k + 1 < bounds.Count; k++)
        {
            int a = bounds[k], b = bounds[k + 1] - 1;
            if (masked[a..b].Trim().Length == 0) return null;
            List<string> piece;
            var nested = Nested().Match(masked[a..b]);
            if (nested.Success)
            {
                var subOpen = a + nested.Length - 1;
                var subClose = LineText.MatchParen(masked, subOpen);
                if (subClose < 0 || masked[(subClose + 1)..b].Trim().Length > 0) return null;
                var sub = Entries(text, masked, subOpen, entryColumn + 2);
                if (sub is null) return null;
                var headEnd = a + nested.Length - 1;
                var nestedHead = ColumnsTail().Replace(LineText.Squash(text[a..headEnd], masked[a..headEnd]), "");
                piece = [nestedHead, new string(' ', entryColumn + 2) + "COLUMNS (" + sub[0], .. sub.Skip(1)];
            }
            else if (ColumnsWord().IsMatch(masked[a..b]))
            {
                return null;
            }
            else
            {
                piece = [LineText.Squash(text[a..b], masked[a..b])];
            }
            if (k < bounds.Count - 2) piece[^1] += ",";
            if (k == 0)
            {
                lines.AddRange(piece);
            }
            else
            {
                lines.Add(new string(' ', entryColumn) + piece[0]);
                lines.AddRange(piece.Skip(1));
            }
        }
        lines[^1] += ")";
        return lines;
    }
}
