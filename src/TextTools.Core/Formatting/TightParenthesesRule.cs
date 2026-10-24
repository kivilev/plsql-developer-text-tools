using System.Text;
using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>No blanks right inside parentheses: <c>( a, b )</c> -> <c>(a, b)</c> (a break after <c>(</c> stays).</summary>
internal static partial class TightParenthesesRule
{
    [GeneratedRegex(@"\((?<blanks>\s+)(?=\S)", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex AfterOpen();

    [GeneratedRegex(@"(?<=\S)(?<blanks>[ \t]+)\)", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex BeforeClose();

    public static string Apply(string text)
    {
        var masked = LineText.Mask(text);
        var spans = new SortedSet<(int Start, int Length)>();
        foreach (Match m in AfterOpen().Matches(masked))
        {
            var blanks = m.Groups["blanks"];
            if (!blanks.Value.Contains('\n', StringComparison.Ordinal)) spans.Add((blanks.Index, blanks.Length));
        }
        foreach (Match m in BeforeClose().Matches(masked))
        {
            var blanks = m.Groups["blanks"];
            spans.Add((blanks.Index, blanks.Length));
        }
        if (spans.Count == 0) return text;
        var sb = new StringBuilder(text);
        foreach (var (start, length) in spans.Reverse()) sb.Remove(start, length);
        return sb.ToString();
    }
}
