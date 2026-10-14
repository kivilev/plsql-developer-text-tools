using System.Globalization;
using System.Text;

namespace TextTools.Core;

/// <summary>Oracle SQL literal helpers.</summary>
public static class SqlText
{
    /// <summary>Oracle allows at most 1000 expressions in an IN list (ORA-01795).</summary>
    public const int InListLimit = 1000;

    public static string Escape(string text) => text.Replace("'", "''");

    /// <summary>Strips '...' or q'[...]' wrapping (if any) and turns '' back into '.</summary>
    public static string Unescape(string text)
    {
        var t = text.Trim();
        if (t.Length >= 5 && (t[0] is 'q' or 'Q') && t[1] == '\'' && t[^1] == '\'')
        {
            var open = t[2];
            var close = open switch { '[' => ']', '{' => '}', '<' => '>', '(' => ')', _ => open };
            if (t[^2] == close) return t[3..^2];
        }
        if (t.Length >= 2 && t[0] == '\'' && t[^1] == '\'') t = t[1..^1];
        else t = text;
        return t.Replace("''", "'");
    }

    /// <summary>Wraps text into an alternative-quoting literal q'[...]', picking a delimiter not used in the text.</summary>
    public static string QQuote(string text)
    {
        foreach (var (open, close) in QDelimiters)
        {
            if (!text.Contains(close + "'", StringComparison.Ordinal)) return $"q'{open}{text}{close}'";
        }
        return Quote(text);
    }

    private static readonly (char Open, char Close)[] QDelimiters =
        [('[', ']'), ('{', '}'), ('<', '>'), ('(', ')'), ('!', '!'), ('~', '~'), ('|', '|'), ('#', '#')];

    public static string Quote(string value) => "'" + Escape(value) + "'";

    /// <summary>
    /// Values → IN (...): one per line (a column pasted from Excel), or a single line split by tab, comma or semicolon
    /// (by spaces when there are none). Already quoted 'values' are not quoted twice.
    /// Numbers stay unquoted. More than 1000 values → IN (SELECT column_value FROM TABLE(sys.odci*list(...))).
    /// </summary>
    public static string ToInList(string text)
    {
        var values = ListValues(text);
        if (values.Count == 0) return text;

        var numeric = values.All(v => !v.Quoted && decimal.TryParse(v.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out _));
        var items = values.Select(v => numeric ? v.Text : Quote(v.Text)).ToList();
        var list = WrapList(items);
        if (values.Count <= InListLimit) return $"IN ({list})";
        var collection = numeric ? "odcinumberlist" : "odcivarchar2list";
        return $"IN (SELECT column_value FROM TABLE(sys.{collection}({list})))";
    }

    private static readonly char[] ListSeparators = ['\t', ',', ';'];
    private static readonly char[] Blanks = [' ', '\t'];

    private static List<(string Text, bool Quoted)> ListValues(string text)
    {
        var lines = Lines.Split(text).Items.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        IEnumerable<string> raw = lines.Count switch
        {
            0 => [],
            > 1 => lines.Select(l => l.TrimEnd(',', ';')),
            _ when lines[0].IndexOfAny(ListSeparators) >= 0 => lines[0].Split(ListSeparators),
            _ => lines[0].Split(Blanks, StringSplitOptions.RemoveEmptyEntries),
        };
        return raw.Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .Select(v => v.Length >= 2 && v[0] == '\'' && v[^1] == '\'' ? (Unescape(v), true) : (v, false))
            .ToList();
    }

    /// <summary>Multiline text → 'line1' || chr(10) || 'line2' — for building dynamic SQL or messages.</summary>
    public static string ToConcatenation(string text)
    {
        var lines = Lines.Split(text).Items;
        return string.Join(" || chr(10) ||" + Environment.NewLine, lines.Select(Quote));
    }

    /// <summary>Joins items with ", " and breaks the line roughly every 100 characters.</summary>
    private static string WrapList(List<string> items)
    {
        var sb = new StringBuilder();
        var lineLen = 0;
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
                lineLen++;
                if (lineLen + items[i].Length > 100)
                {
                    sb.Append(Environment.NewLine);
                    lineLen = 0;
                }
                else
                {
                    sb.Append(' ');
                    lineLen++;
                }
            }
            sb.Append(items[i]);
            lineLen += items[i].Length;
        }
        return sb.ToString();
    }
}
