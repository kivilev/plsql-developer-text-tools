using System.Text;
using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>A unary minus right after <c>(</c> or a comma is written without a blank: <c>IN (- 4068, - 4061)</c> -> <c>IN (-4068, -4061)</c>.</summary>
internal static partial class UnaryMinusRule
{
    [GeneratedRegex(@"[(,][ \t]*-(?<blanks>[ \t]+)(?=[\w(])", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex SpacedMinus();

    public static string Apply(string text)
    {
        var masked = LineText.Mask(text);
        var spans = SpacedMinus().Matches(masked).Select(m => m.Groups["blanks"]).ToList();
        if (spans.Count == 0) return text;
        var sb = new StringBuilder(text);
        for (var k = spans.Count - 1; k >= 0; k--) sb.Remove(spans[k].Index, spans[k].Length);
        return sb.ToString();
    }
}
