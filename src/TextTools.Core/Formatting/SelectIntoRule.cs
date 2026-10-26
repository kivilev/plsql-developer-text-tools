using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>
/// R15, the exception for SELECT ... INTO: the select list and the INTO list, each on its own, go back on one line when they
/// fit in the margin; otherwise they keep one item per line. <see cref="JoinIntoList"/> first puts a bare <c>INTO</c> next to its list.
/// </summary>
internal static partial class SelectIntoRule
{
    [GeneratedRegex(@"^(?<indent>\s*)INTO\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex BareInto();

    [GeneratedRegex(@"^(?:FROM|WHERE|SELECT|INTO|ORDER|GROUP|HAVING|UNION|VALUES|EXCEPTION|LOOP|BEGIN|END|RETURNING|FETCH|AND|OR)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ClauseKeyword();

    [GeneratedRegex(@"^(?<indent>\s*)SELECT\b(?!\s*\*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex SelectStart();

    [GeneratedRegex(@"\b(?:INTO|FROM)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex IntoOrFrom();

    [GeneratedRegex(@"^\s*INTO\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex IntoLine();

    [GeneratedRegex(@"^\s*(?:INTO|FROM)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex IntoOrFromLine();

    [GeneratedRegex(@"^\s*FROM\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex FromLine();

    private const int ListLines = 40;

    /// <summary><c>INTO</c> alone on its line: the first item of the list moves next to it, the others line up under it.</summary>
    public static string JoinIntoList(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = (string[])lines.Clone();
        var skip = new HashSet<int>();
        for (var i = 0; i < lines.Length; i++)
        {
            var m = BareInto().Match(masked[i]);
            if (!m.Success) continue;
            var indent = m.Groups["indent"].Length;
            var items = new List<int>();
            for (var j = i + 1; j < lines.Length; j++)
            {
                var st = masked[j].Trim();
                if (st.Length == 0 || ClauseKeyword().IsMatch(st) || LineText.Indent(masked[j]) != indent) break;
                items.Add(j);
                if (!masked[j].TrimEnd().EndsWith(',')) break;
            }
            if (items.Count == 0 || masked[items[^1]].Trim().Length == 0) continue;
            output[i] = new string(' ', indent) + "INTO " + lines[items[0]].Trim();
            skip.Add(items[0]);
            foreach (var k in items.Skip(1)) output[k] = new string(' ', indent + 5) + lines[k].Trim();
        }
        return string.Join('\n', output.Where((_, i) => !skip.Contains(i)));
    }

    /// <summary>SELECT list and INTO list, each back on one line when it fits in <paramref name="margin"/>.</summary>
    public static string JoinShort(string text, int margin)
    {
        var (lines, masked) = LineText.Split(text);
        var output = new List<string>(lines.Length);
        var i = 0;
        while (i < lines.Length)
        {
            var s = SelectStart().Match(masked[i]);
            if (!s.Success || IntoOrFrom().IsMatch(masked[i]))
            {
                output.Add(lines[i]);
                i++;
                continue;
            }
            var j = i + 1;
            while (j < lines.Length && j - i < ListLines && !IntoOrFromLine().IsMatch(masked[j])) j++;
            if (j >= lines.Length || !IntoLine().IsMatch(masked[j]))
            {
                output.Add(lines[i]);
                i++;
                continue;
            }
            var k = j + 1;
            while (k < lines.Length && k - j < ListLines && !FromLine().IsMatch(masked[k])) k++;
            var select = Enumerable.Range(i, j - i).ToList();
            var into = Enumerable.Range(j, k - j).ToList();
            if (k >= lines.Length || select.Concat(into).Any(x => LineText.HasComment(lines[x])))
            {
                output.Add(lines[i]);
                i++;
                continue;
            }
            var joinedSelect = Joined(lines, masked, select, margin);
            var joinedInto = Joined(lines, masked, into, margin);
            output.AddRange(joinedSelect is not null ? [joinedSelect] : select.Select(x => lines[x]));
            output.AddRange(joinedInto is not null ? [joinedInto] : into.Select(x => lines[x]));
            i = k;
        }
        return string.Join('\n', output);
    }

    /// <summary>Rows (one list item per line, all but the last ending with a comma) as one line, or null when it does not fit.</summary>
    private static string? Joined(string[] lines, string[] masked, List<int> rows, int margin)
    {
        if (rows.Count < 2 || rows.Any(x => LineText.Net(masked[x]) != 0)) return null;
        if (rows.Take(rows.Count - 1).Any(x => !masked[x].TrimEnd().EndsWith(','))) return null;
        var joined = string.Join(' ', new[] { lines[rows[0]].TrimEnd() }.Concat(rows.Skip(1).Select(x => lines[x].Trim())));
        joined = LineText.Squash(joined, LineText.Mask(joined));
        var indent = lines[rows[0]][..LineText.Indent(lines[rows[0]])];
        joined = indent + joined;
        return joined.Length <= margin ? joined : null;
    }
}
