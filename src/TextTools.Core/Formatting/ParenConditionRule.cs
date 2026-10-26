using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>R14: a parenthesised condition (<c>AND (a = b OR c IS NULL)</c>) that fits the margin goes back on one line.</summary>
internal static partial class ParenConditionRule
{
    [GeneratedRegex(@"^\s*(?:AND|OR|WHERE|ON|IF|ELSIF|WHEN)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ConditionStart();

    [GeneratedRegex(@"^\s*(?:AND|OR)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex AndOr();

    [GeneratedRegex(@"\bSELECT\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Select();

    private const int GroupLines = 8;

    public static string Apply(string text, int margin)
    {
        var (lines, masked) = LineText.Split(text);
        var output = new List<string>(lines.Length);
        var i = 0;
        while (i < lines.Length)
        {
            var balance = LineText.Net(masked[i]);
            if (balance > 0 && ConditionStart().IsMatch(masked[i]))
            {
                var j = i;
                var open = balance;
                var closed = false;
                while (j + 1 < lines.Length && j - i < GroupLines)
                {
                    j++;
                    if (!AndOr().IsMatch(masked[j])) break;
                    open += LineText.Net(masked[j]);
                    if (open == 0)
                    {
                        closed = true;
                        break;
                    }
                    if (open < 0) break;
                }
                var block = lines.Skip(i).Take(j - i + 1).ToList();
                if (closed && !block.Any(LineText.HasComment) && !Select().IsMatch(string.Join(' ', masked.Skip(i).Take(j - i + 1))))
                {
                    var joined = lines[i].TrimEnd() + " " + string.Join(' ', lines.Skip(i + 1).Take(j - i).Select(x => x.Trim()));
                    if (joined.TrimEnd().Length <= margin)
                    {
                        output.Add(joined);
                        i = j + 1;
                        continue;
                    }
                }
            }
            output.Add(lines[i]);
            i++;
        }
        return string.Join('\n', output);
    }
}
