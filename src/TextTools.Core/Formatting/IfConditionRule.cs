using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>R11: an IF / ELSIF condition that the Beautifier broke over several lines is one logical line again (the wrap rule breaks it by the margin).</summary>
internal static partial class IfConditionRule
{
    [GeneratedRegex(@"^(?<indent>\s*)(?:IF|ELSIF)\b(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex IfStart();

    [GeneratedRegex(@"\bTHEN\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Then();

    [GeneratedRegex(@"^\s*(?:RETURN\b|[A-Za-z_][\w$#.()]*\s*:=)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex BooleanHead();

    [GeneratedRegex(@"^\s*(?:AND|OR)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex AndOrStart();

    [GeneratedRegex(@"\bSELECT\b|--|/\*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex SelectOrComment();

    [GeneratedRegex(@"^(?<indent>\s*)(?<keyword>IF|ELSIF|WHILE|WHEN|FOR\s+[\w$#]+\s+IN)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex OpenerStart();

    [GeneratedRegex(@"\b(?:THEN|LOOP)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ThenOrLoop();

    [GeneratedRegex(@"\b(?:THEN|LOOP)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ThenOrLoopAtEnd();

    [GeneratedRegex(@"\sTHEN\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ThenAtEnd();

    [GeneratedRegex(@"\sLOOP\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex LoopAtEnd();

    private const int ConditionLines = 14;
    private const int OpenerLines = 400;

    /// <summary>RETURN a / OR b; and x := a / AND b; (broken before AND / OR) are one line again: the wrap rule breaks them by the margin.</summary>
    public static string JoinBooleanExpressions(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = new List<string>(lines.Length);
        var i = 0;
        while (i < lines.Length)
        {
            if (BooleanHead().IsMatch(masked[i]) && !masked[i].Contains(';', StringComparison.Ordinal) && !SelectOrComment().IsMatch(masked[i]) && !LineText.HasComment(lines[i]))
            {
                var j = i + 1;
                while (j < lines.Length && j - i < ConditionLines && AndOrStart().IsMatch(masked[j]) && !masked[j].Contains(';', StringComparison.Ordinal)) j++;
                var start = i;
                if (j < lines.Length && AndOrStart().IsMatch(masked[j]) && masked[j].Contains(';', StringComparison.Ordinal)
                    && !Enumerable.Range(start, j - start + 1).Any(x => SelectOrComment().IsMatch(masked[x]) || LineText.HasComment(lines[x])))
                {
                    output.Add(lines[i].TrimEnd() + " " + string.Join(' ', lines.Skip(i + 1).Take(j - i).Select(x => x.Trim())));
                    i = j + 1;
                    continue;
                }
            }
            output.Add(lines[i]);
            i++;
        }
        return string.Join('\n', output);
    }

    /// <summary>IF / ELSIF / WHILE / WHEN / FOR whose condition takes several lines: THEN / LOOP goes on its own line under the keyword.</summary>
    public static string ThenOnOwnLine(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = new List<string>(lines.Length);
        var i = 0;
        while (i < lines.Length)
        {
            var m = OpenerStart().Match(masked[i]);
            if (!m.Success || ThenOrLoop().IsMatch(masked[i]) || masked[i].Contains(';', StringComparison.Ordinal))
            {
                output.Add(lines[i]);
                i++;
                continue;
            }
            var then = m.Groups["keyword"].Value.ToUpperInvariant() is "IF" or "ELSIF" or "WHEN";
            // The THEN of a CASE inside the condition is not the closer: lines are looked at outside CASE ... END only.
            var j = i + 1;
            var caseDepth = CaseBalance(masked[i]);
            while (j < lines.Length && j - i < OpenerLines && !masked[j].Contains(';', StringComparison.Ordinal))
            {
                var inCase = caseDepth > 0;
                caseDepth += CaseBalance(masked[j]);
                if (!inCase && caseDepth <= 0 && ThenOrLoopAtEnd().IsMatch(masked[j])) break;
                j++;
            }
            var closer = then ? ThenAtEnd() : LoopAtEnd();
            var end = j < lines.Length && caseDepth <= 0 ? closer.Match(masked[j]) : Match.Empty;
            if (!end.Success || masked[j][..end.Index].Trim().Length == 0)
            {
                output.Add(lines[i]);
                i++;
                continue;
            }
            output.AddRange(lines.Skip(i).Take(j - i));
            output.Add(lines[j][..end.Index].TrimEnd());
            output.Add(m.Groups["indent"].Value + lines[j][end.Index..].Trim());
            i = j + 1;
        }
        return string.Join('\n', output);
    }

    [GeneratedRegex(@"\bCASE\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex CaseWord();

    [GeneratedRegex(@"\bEND\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex EndWord();

    /// <summary>CASE opened minus END closed on a masked line (an expression CASE inside a condition).</summary>
    private static int CaseBalance(string masked) => CaseWord().Count(masked) - EndWord().Count(masked);

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
