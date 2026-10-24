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
}
