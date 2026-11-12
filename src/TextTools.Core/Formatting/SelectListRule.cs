using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>R15: a select list of one column per line; the first column after a comment gets the indent of the others; alias columns are not padded past the hard limit.</summary>
internal static partial class SelectListRule
{
    [GeneratedRegex(@"^(?<indent>\s*)SELECT\s+(?<modifier>(?:DISTINCT\s+|UNIQUE\s+)?)(?!\*)(?<list>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex SelectLine();

    [GeneratedRegex(@"\b(?:FROM|INTO)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex FromOrInto();

    [GeneratedRegex(@"^\s*SELECT\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex BareSelect();

    [GeneratedRegex(@"^\s*(?:FROM|INTO)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex FromOrIntoLine();

    [GeneratedRegex(@"\S(?<pad> {2,})(?:AS\s+)?[\w$#]+,?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex AliasPadding();

    [GeneratedRegex(@"(?<last>\S) {2,}(?<alias>(?:AS\s+)?[\w$#]+,?\s*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex PaddedAlias();

    /// <summary><c>SELECT a, b, c</c> on one line (no FROM on it): one column per line under the first one.</summary>
    public static string SplitLine(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = new List<string>(lines.Length);
        for (var n = 0; n < lines.Length; n++)
        {
            string line = lines[n], m = masked[n];
            var s = SelectLine().Match(m);
            if (!s.Success || FromOrInto().IsMatch(m) || LineText.HasComment(line))
            {
                output.Add(line);
                continue;
            }
            var a = s.Groups["list"].Index;
            var b = m.TrimEnd().Length;
            var cuts = LineText.SplitTop(m, a, b);
            if (cuts.Count == 0 || m.TrimEnd().EndsWith(',') || LineText.Net(m) != 0)
            {
                output.Add(line);
                continue;
            }
            var bounds = new List<int> { a };
            bounds.AddRange(cuts.Select(c => c + 1));
            bounds.Add(b + 1);
            var items = new List<string>();
            for (var k = 0; k < bounds.Count - 1; k++)
            {
                var from = bounds[k];
                var to = Math.Min(bounds[k + 1] - 1, line.Length);
                items.Add(LineText.Squash(line[from..to], m[from..to]));
            }
            if (items.Count < 2)
            {
                output.Add(line);
                continue;
            }
            output.Add(line[..a] + items[0] + ",");
            for (var k = 1; k < items.Count; k++) output.Add(new string(' ', a) + items[k] + (k < items.Count - 1 ? "," : ""));
        }
        return string.Join('\n', output);
    }

    /// <summary>SELECT alone on its line, comment lines, then the first column: it takes the indent of the columns that follow.</summary>
    public static string AfterComment(string text)
    {
        var (lines, masked) = LineText.Split(text);
        for (var i = 0; i < lines.Length; i++)
        {
            if (!BareSelect().IsMatch(masked[i])) continue;
            var k = i + 1;
            while (k < lines.Length && LineText.IsCommentOnly(lines[k])) k++;
            if (k == i + 1 || k + 1 >= lines.Length) continue;
            var next = k + 1;
            while (next < lines.Length && LineText.IsCommentOnly(lines[next])) next++;
            if (next >= lines.Length || FromOrIntoLine().IsMatch(masked[next])) continue;
            var indent = LineText.Indent(lines[next]);
            for (var x = i + 1; x <= k; x++) lines[x] = new string(' ', indent) + lines[x].TrimStart();
        }
        return string.Join('\n', lines);
    }

    /// <summary>The aliases of a select list are not aligned: one blank before each alias.</summary>
    public static string UnalignAliases(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = (string[])lines.Clone();
        var inList = false; // only between SELECT and FROM / INTO: elsewhere "name  type" is a declaration, not an alias
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = masked[i].TrimStart();
            if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) inList = true;
            else if (trimmed.StartsWith("FROM", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("INTO", StringComparison.OrdinalIgnoreCase)) inList = false;
            if (inList && AliasPadding().IsMatch(masked[i])) output[i] = PaddedAlias().Replace(lines[i], "${last} ${alias}", 1);
        }
        return string.Join('\n', output);
    }

    /// <summary>
    /// The columns of the aliases the Beautifier aligned stay aligned, except for a group of consecutive select list lines that
    /// the alignment pushes past the hard limit of the code standard (120): in that group every alias gets one blank before it.
    /// </summary>
    public static string TightenAliasPadding(string text, int hard = 120)
    {
        var (lines, masked) = LineText.Split(text);
        var output = (string[])lines.Clone();
        var inList = new bool[lines.Length]; // from SELECT up to, not including, FROM / INTO
        var inside = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = masked[i].TrimStart();
            if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                inside = !ClauseEnd().IsMatch(masked[i]);
                inList[i] = true;
            }
            else if (inside && (trimmed.StartsWith("FROM", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("INTO", StringComparison.OrdinalIgnoreCase)))
            {
                inside = false;
            }
            else
            {
                inList[i] = inside;
            }
        }
        var n = 0;
        while (n < lines.Length)
        {
            if (!(inList[n] && AliasPadding().IsMatch(masked[n])))
            {
                n++;
                continue;
            }
            var last = n;
            while (last + 1 < lines.Length && inList[last + 1] && AliasPadding().IsMatch(masked[last + 1])) last++;
            if (Enumerable.Range(n, last - n + 1).Max(k => lines[k].TrimEnd().Length) > hard)
            {
                for (var k = n; k <= last; k++) output[k] = PaddedAlias().Replace(lines[k], "${last} ${alias}", 1);
            }
            n = last + 1;
        }
        return string.Join('\n', output);
    }

    [GeneratedRegex(@"\b(?:FROM|INTO)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ClauseEnd();

    [GeneratedRegex(@"\bSELECT\s+(?:(?:DISTINCT|UNIQUE|ALL)\s+)?(?!\*)|^\s*(?:BULK\s+COLLECT\s+)?INTO\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ListHead();

    [GeneratedRegex(@"^\s*(?:FROM|INTO|BULK\s+COLLECT)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ListEnd();

    [GeneratedRegex(@"^\s*SELECT\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex SelectStart();

    [GeneratedRegex(@"^SELECT\s+(?:DISTINCT|UNIQUE|ALL)\s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex SelectModifier();

    [GeneratedRegex(@"^\s*CREATE\b.*\bVIEW\b.*\(\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ViewHeaderOpen();

    /// <summary>
    /// The column list of a SELECT and its INTO list packed up to the margin, the continuation under the first column (after
    /// <c>SELECT DISTINCT</c> under DISTINCT); a multi-line item (CASE, subquery) keeps its lines, a list with a comment stays.
    /// </summary>
    public static string Fill(string text, int margin)
    {
        var (lines, masked) = LineText.Split(text);
        var output = new List<string>(lines.Length);
        var i = 0;
        while (i < lines.Length)
        {
            var heads = ListHead().Matches(masked[i]);
            var last = heads.Count > 0 ? heads[^1] : null;
            var column = last is null ? -1 : last.Index + last.Length;
            var j = i + 1;
            var depth = last is null ? 0 : LineText.Net(masked[i][column..]);
            while (last is not null && j < lines.Length && !(depth == 0 && ListEnd().IsMatch(masked[j])) && !masked[j].Contains(';', StringComparison.Ordinal))
            {
                depth += LineText.Net(masked[j]);
                j++;
            }
            var ok = last is not null && j > i + 1 && j < lines.Length && depth == 0 && ListEnd().IsMatch(masked[j]) && !masked[i].Contains(';', StringComparison.Ordinal);
            ok = ok && !Enumerable.Range(i + 1, Math.Max(0, j - i - 1)).Any(x => SelectStart().IsMatch(masked[x])); // INSERT / INTO t / SELECT ...: not a list
            if (!ok || FromOrInto().IsMatch(masked[i][column..]) || Enumerable.Range(i, j - i).Any(x => LineText.HasComment(lines[x])))
            {
                output.Add(lines[i]);
                i++;
                continue;
            }
            var continuation = SelectModifier().IsMatch(masked[i][last!.Index..]) ? last.Index + 7 : column;
            output.AddRange(FillRows(lines, masked, [.. Enumerable.Range(i, j - i)], column, margin, continuation));
            i = j;
        }
        return string.Join('\n', output);
    }

    /// <summary>The column list in the header of a view (<c>CREATE VIEW v (</c> ... <c>)</c>) packed up to the margin.</summary>
    public static string FillViewColumns(string text, int margin)
    {
        var (lines, masked) = LineText.Split(text);
        var output = new List<string>(lines.Length);
        var i = 0;
        while (i < lines.Length)
        {
            output.Add(lines[i]);
            i++;
            if (!ViewHeaderOpen().IsMatch(masked[i - 1])) continue;
            var j = i;
            while (j < lines.Length && !masked[j].TrimStart().StartsWith(')') && !masked[j].Contains(';', StringComparison.Ordinal)) j++;
            if (i < j && j < lines.Length && masked[j].TrimStart().StartsWith(')') && !Enumerable.Range(i, j - i).Any(x => LineText.HasComment(lines[x])))
            {
                var column = LineText.Indent(lines[i]);
                output.AddRange(FillRows(lines, masked, [.. Enumerable.Range(i, j - i)], column, margin, column));
                i = j;
            }
        }
        return string.Join('\n', output);
    }

    /// <summary>The list items of <paramref name="rows"/> packed up to the margin, the continuation at <paramref name="continuation"/>; an item of several lines keeps them.</summary>
    private static List<string> FillRows(string[] lines, string[] masked, List<int> rows, int column, int margin, int continuation)
    {
        var items = new List<List<int>>();
        var item = new List<int>();
        var depth = 0;
        foreach (var x in rows)
        {
            depth += LineText.Net(x == rows[0] ? masked[x][column..] : masked[x]);
            item.Add(x);
            if (depth == 0 && masked[x].TrimEnd().EndsWith(','))
            {
                items.Add(item);
                item = [];
            }
        }
        if (item.Count > 0) items.Add(item);
        var output = new List<string>();
        var current = new System.Text.StringBuilder(lines[rows[0]][..column]);
        var empty = true;
        foreach (var it in items)
        {
            if (it.Count > 1)
            {
                if (!empty) output.Add(current.ToString());
                output.AddRange(it.Select(x => lines[x]));
                current.Clear().Append(' ', continuation);
                empty = true;
                continue;
            }
            var x = it[0];
            var piece = x == rows[0] ? LineText.Squash(lines[x][column..], masked[x][column..]) : LineText.Squash(lines[x], masked[x]);
            if (empty)
            {
                current.Append(piece);
                empty = false;
            }
            else if (current.Length + 1 + piece.Length > margin)
            {
                output.Add(current.ToString());
                current.Clear().Append(' ', continuation).Append(piece);
            }
            else
            {
                current.Append(' ').Append(piece);
            }
        }
        if (!empty) output.Add(current.ToString());
        return output;
    }
}
