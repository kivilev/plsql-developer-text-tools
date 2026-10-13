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
    /// One value per line (e.g. a column pasted from Excel) → IN (...).
    /// Numbers stay unquoted. More than 1000 values → IN (SELECT column_value FROM TABLE(sys.odci*list(...))).
    /// </summary>
    public static string ToInList(string text)
    {
        var values = Lines.Split(text).Items
            .Select(v => v.Trim().TrimEnd(','))
            .Where(v => v.Length > 0)
            .ToList();
        if (values.Count == 0) return text;

        var numeric = values.All(v => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out _));
        var items = values.Select(v => numeric ? v : Quote(v)).ToList();
        var list = WrapList(items);
        if (values.Count <= InListLimit) return $"IN ({list})";
        var collection = numeric ? "odcinumberlist" : "odcivarchar2list";
        return $"IN (SELECT column_value FROM TABLE(sys.{collection}({list})))";
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
