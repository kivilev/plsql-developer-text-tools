using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>
/// The alias of a FROM / JOIN source after one blank, not aligned in a column (<c>FROM deposit_access c,</c>, <c>JOIN s_val v</c>,
/// <c>) d</c> before ON); <c>JOIN (subquery) alias</c> then <c>ON</c>: the ON and its AND / OR right-aligned to that JOIN.
/// </summary>
internal static partial class TableAliasRule
{
    private const string JoinWords = @"(?:(?:LEFT|RIGHT|FULL|INNER|CROSS|NATURAL)\s+)?(?:OUTER\s+)?JOIN";

    [GeneratedRegex(@"^(?<lead>\s*(?:FROM|" + JoinWords + @")?\s*)(?<source>[\w$#.""]+|\))(?<pad> {2,})(?<alias>[\w$#]+,?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex FromItem();

    [GeneratedRegex(@"^\s*(?:ON|USING|WHERE|GROUP|ORDER|CONNECT|START|HAVING|UNION|MINUS|INTERSECT|" + JoinWords + @"|\)|;)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex AfterSource();

    [GeneratedRegex(@"^(?:FROM|" + JoinWords + @")\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex SourceHead();

    [GeneratedRegex(@"\)(?<pad> {2,})[\w$#]+\s*$", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex InlineViewAlias();

    [GeneratedRegex(@"\bFROM\s+(?=[\w$#.""]+\s+[\w$#]+\s*,)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex FromList();

    [GeneratedRegex(@"\b(?:WHERE|GROUP|ORDER|CONNECT|START)\b|[;)]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex FromListEnd();

    [GeneratedRegex(@"(?<=[\w$#""]) {2,}(?=[\w$#]+\s*(?:,|$))", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ListPadding();

    [GeneratedRegex(@"^\s*ON\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex OnStart();

    [GeneratedRegex(@"\)\s*[\w$#]+\s*$", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ParenAlias();

    [GeneratedRegex(@"^(?<indent>\s*)(?<kind>(?:LEFT|RIGHT|INNER|FULL|CROSS|NATURAL)\s+(?:OUTER\s+)?)?JOIN\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex JoinHead();

    [GeneratedRegex(@"^\s*(?:AND|OR)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex AndOrStart();

    public static string Apply(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = (string[])lines.Clone();
        var inFrom = false; // inside a comma separated FROM list
        for (var i = 0; i < masked.Length; i++)
        {
            var m = masked[i];
            var st = m.Trim();
            var head = SourceHead().Match(st);
            var next = "";
            for (var k = i + 1; k < masked.Length; k++)
            {
                if (masked[k].Trim().Length == 0) continue;
                next = masked[k];
                break;
            }
            var fm = FromItem().Match(m);
            var source = head.Success || inFrom || (fm.Success && string.Equals(fm.Groups["source"].Value, ")", StringComparison.Ordinal) && AfterSource().IsMatch(next));
            if (fm.Success && source && (head.Success || fm.Groups["lead"].Value.Trim().Length == 0))
            {
                var pad = fm.Groups["pad"];
                output[i] = lines[i][..pad.Index] + " " + lines[i][(pad.Index + pad.Length)..];
            }
            else if (!fm.Success)
            {
                var sub = InlineViewAlias().Match(m);
                if (sub.Success && AfterSource().IsMatch(next))
                {
                    var pad = sub.Groups["pad"];
                    output[i] = lines[i][..pad.Index] + " " + lines[i][(pad.Index + pad.Length)..];
                }
            }
            // FROM a l, b           c on one line: the padding inside the list.
            var fr = FromList().Match(m);
            if (fr.Success && string.Equals(output[i], lines[i], StringComparison.Ordinal))
            {
                var from = fr.Index + fr.Length;
                var seg = FromListEnd().Match(m[from..]);
                var e = from + (seg.Success ? seg.Index : m.Length - from);
                foreach (var sp in ListPadding().Matches(m[from..e]).Reverse())
                {
                    output[i] = output[i][..(from + sp.Index)] + " " + output[i][(from + sp.Index + sp.Length)..];
                }
            }
            if (head.Success && st.StartsWith("FROM", StringComparison.OrdinalIgnoreCase)) inFrom = st.EndsWith(',');
            else if (inFrom) inFrom = st.EndsWith(',');
        }
        return string.Join('\n', output);
    }

    /// <summary><c>JOIN (subquery) alias</c>, then <c>ON</c>: ON and its AND / OR right-aligned to that JOIN, as after a table.</summary>
    public static string OnAfterSubquery(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var whole = string.Join('\n', masked);
        var starts = new int[lines.Length + 1];
        for (var i = 0; i < lines.Length; i++) starts[i + 1] = starts[i] + lines[i].Length + 1;
        var output = (string[])lines.Clone();
        for (var i = 1; i < lines.Length; i++)
        {
            if (!OnStart().IsMatch(masked[i])) continue;
            var previous = ParenAlias().Match(masked[i - 1]);
            if (!previous.Success) continue;
            int pos = starts[i - 1] + previous.Index, depth = 0;
            while (pos >= 0)
            {
                var c = whole[pos];
                if (c == ')') depth++;
                else if (c == '(') depth--;
                if (depth == 0) break;
                pos--;
            }
            if (pos < 0) continue;
            var line = whole.AsSpan(0, pos).Count('\n');
            var j = JoinHead().Match(masked[line]);
            if (!j.Success || whole[(starts[line] + j.Length)..pos].Trim().Length > 0) continue;
            var edge = j.Groups["indent"].Length + j.Groups["kind"].Length + "JOIN".Length;
            if (edge < 2) continue;
            output[i] = new string(' ', edge - 2) + lines[i].TrimStart();
            for (var k = i + 1; k < lines.Length && AndOrStart().IsMatch(masked[k]); k++)
            {
                var width = masked[k].TrimStart().StartsWith("AND", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
                if (edge >= width) output[k] = new string(' ', edge - width) + lines[k].TrimStart();
            }
        }
        return string.Join('\n', output);
    }
}
