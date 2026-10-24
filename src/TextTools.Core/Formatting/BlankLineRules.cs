using System.Text.RegularExpressions;
using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>R19 and R20: blank lines around subprograms.</summary>
internal static partial class BlankLineRules
{
    [GeneratedRegex(@"^\s*(?:PROCEDURE|FUNCTION)\s+\w", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Subprogram();

    [GeneratedRegex(@"^\s*(?:--|/\*)", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex CommentOnly();

    [GeneratedRegex(@"\b(?:IS|AS)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex EndsWithIsOrAs();

    [GeneratedRegex(@"^END\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ClosingEnd();

    /// <summary>R19: a blank line before each PROCEDURE/FUNCTION and before the comment block above it; not right after IS/AS; right after the header CREATE ... IS only with <paramref name="afterHeader"/>.</summary>
    public static string BeforeSubprograms(string text, bool afterHeader = false)
    {
        var lines = text.Split('\n');
        var masked = PlsqlLexer.Mask(text).Split('\n');
        var output = new List<string>(lines.Length + 16);
        for (var i = 0; i < lines.Length; i++)
        {
            if (Subprogram().IsMatch(masked[i]))
            {
                var k = output.Count;
                while (k > 0 && CommentOnly().IsMatch(output[k - 1])) k--;
                var previous = k > 0 ? output[k - 1] : "";
                var maskedPrevious = PlsqlLexer.Mask(previous).Trim();
                var header = maskedPrevious.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase);
                if (k > 0 && previous.Trim().Length > 0 && (header ? afterHeader && EndsWithIsOrAs().IsMatch(maskedPrevious) : !EndsWithIsOrAs().IsMatch(maskedPrevious)))
                {
                    output.Insert(k, "");
                }
            }
            output.Add(lines[i]);
        }
        return string.Join('\n', output);
    }

    /// <summary>R20: no blank line right before an END at column 0 (the end of a package or type body).</summary>
    public static string NoBlankBeforePackageEnd(string text)
    {
        var output = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            if (ClosingEnd().IsMatch(line))
            {
                while (output.Count > 0 && output[^1].Trim().Length == 0) output.RemoveAt(output.Count - 1);
            }
            output.Add(line);
        }
        return string.Join('\n', output);
    }
}
