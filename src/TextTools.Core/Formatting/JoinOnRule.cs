using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>R12 under a JOIN: <c>ON</c> and the <c>AND</c> / <c>OR</c> lines after it have their keywords right-aligned to the end of the JOIN keyword.</summary>
internal static partial class JoinOnRule
{
    [GeneratedRegex(@"^(?<indent>\s*)(?<kind>(?:LEFT|RIGHT|INNER|FULL|CROSS|NATURAL)\s+(?:OUTER\s+)?)?JOIN\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Join();

    [GeneratedRegex(@"^ON\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex OnWord();

    [GeneratedRegex(@"^(?:AND|OR)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex AndOrWord();

    public static string Apply(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = (string[])lines.Clone();
        var i = 0;
        while (i < lines.Length)
        {
            var m = Join().Match(masked[i]);
            if (!m.Success)
            {
                i++;
                continue;
            }
            var edge = m.Groups["indent"].Length + (m.Groups["kind"].Value + "JOIN").Length;
            var j = i + 1;
            var first = true;
            while (j < lines.Length)
            {
                var st = masked[j].Trim();
                if (first && OnWord().IsMatch(st))
                {
                    output[j] = new string(' ', edge - 2) + lines[j].TrimStart();
                    first = false;
                }
                else if (!first && AndOrWord().IsMatch(st))
                {
                    var width = st[..3].Equals("AND", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
                    output[j] = new string(' ', Math.Max(0, edge - width)) + lines[j].TrimStart();
                }
                else
                {
                    break;
                }
                j++;
            }
            i = j;
        }
        return string.Join('\n', output);
    }
}
