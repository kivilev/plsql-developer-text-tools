using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>R11: an IF / ELSIF condition that the Beautifier broke over several lines is one logical line again (the wrap rule breaks it by the margin).</summary>
internal static partial class IfConditionRule
{
    [GeneratedRegex(@"^(?<indent>\s*)(?:IF|ELSIF)\b(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex IfStart();

    [GeneratedRegex(@"\bTHEN\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Then();

    private const int ConditionLines = 14;

    public static string Apply(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = new List<string>(lines.Length);
        var i = 0;
        while (i < lines.Length)
        {
            var m = IfStart().Match(masked[i]);
            if (m.Success && !Then().IsMatch(masked[i]) && !masked[i].TrimEnd().EndsWith(';'))
            {
                var j = i;
                var ok = false;
                while (j < lines.Length && j - i < ConditionLines)
                {
                    if (Then().IsMatch(masked[j]))
                    {
                        ok = true;
                        break;
                    }
                    if (masked[j].Contains(';', StringComparison.Ordinal)) break;
                    j++;
                }
                var block = lines.Skip(i).Take(j - i + 1).ToList();
                if (ok && !block.Any(x => x.Contains("--", StringComparison.Ordinal)))
                {
                    output.Add(m.Groups["indent"].Value + string.Join(' ', block.Select(x => x.Trim())));
                    i = j + 1;
                    continue;
                }
            }
            output.Add(lines[i]);
            i++;
        }
        return string.Join('\n', output);
    }
}
