using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>R18 tail: the end of a PROCEDURE / FUNCTION header (<c>RESULT_CACHE</c>, <c>AUTHID CURRENT_USER</c>, <c>IS</c>, ...) is joined to the line it belongs to.</summary>
internal static partial class HeaderTailsRule
{
    [GeneratedRegex(@"^(?:CREATE\s+(?:OR\s+REPLACE\s+)?)?(?:PROCEDURE|FUNCTION)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex HeaderStart();

    [GeneratedRegex(@"\b(?:IS|AS)\b|;", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex IsAsOrSemicolon();

    [GeneratedRegex(@"^(?:RESULT_CACHE|DETERMINISTIC|PIPELINED|PARALLEL_ENABLE|AUTHID\b.*|IS|AS)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Tail();

    [GeneratedRegex(@"\b(?:IS|AS)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex EndsWithIsAs();

    public static string Apply(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = new List<string>(lines.Length);
        var open = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var st = masked[i].Trim();
            if (HeaderStart().IsMatch(st) && !IsAsOrSemicolon().IsMatch(st))
            {
                open = true;
                output.Add(lines[i]);
                continue;
            }
            if (open && output.Count > 0 && Tail().IsMatch(st))
            {
                output[^1] = output[^1].TrimEnd() + " " + lines[i].Trim();
                if (EndsWithIsAs().IsMatch(output[^1])) open = false;
                continue;
            }
            if (open && IsAsOrSemicolon().IsMatch(st)) open = false;
            output.Add(lines[i]);
        }
        return string.Join('\n', output);
    }
}
