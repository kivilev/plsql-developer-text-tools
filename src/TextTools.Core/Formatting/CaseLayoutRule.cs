using System.Text;
using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>
/// The layout of CASE expressions. <see cref="JoinShort"/>: a CASE with one WHEN that fits in 70 columns goes on one line, with
/// the <c>(</c> / <c>=&gt;</c> / <c>:=</c> before it and the <c>)</c> after it. <see cref="Relayout"/>: a CASE spread over lines gets
/// WHEN / ELSE two columns right of CASE, the value two more, END under CASE; a condition past the margin one condition per line.
/// <see cref="ExpandLong"/>: a one-line CASE that ends up past 120 goes back on lines. CASE statements, nested CASE, subqueries and
/// comments are left alone.
/// </summary>
internal static partial class CaseLayoutRule
{
    private const int OneLine = 70;
    private const int Hard = 120;

    [GeneratedRegex(@"\bCASE\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Case();

    [GeneratedRegex(@"\bEND\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex EndBefore();

    [GeneratedRegex(@"\b(?:CASE|END|WHEN|SELECT)\b|;", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex CaseToken();

    [GeneratedRegex(@"^\s*(?:CASE|IF|LOOP)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex StatementEnd();

    [GeneratedRegex(@"(?<op>\(|=>|:=)(?<gap>[ \t]*\n\s*)$", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex OpenerBefore();

    [GeneratedRegex(@"^\s*\n\s*\)", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex CloserAfter();

    [GeneratedRegex(@"\|\|(?<gap>[ \t]*\n\s*)$", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ConcatBefore();

    [GeneratedRegex(@"^[ \t]*\n\s*(?=\|\|)", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ConcatAfter();

    [GeneratedRegex(@"\b(?:CASE|END|WHEN|THEN|ELSE|SELECT)\b|[;(),]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex CasePart();

    [GeneratedRegex(@"\bCASE (?<when>WHEN .+?) (?<else>ELSE .+?) END\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex OneLineCase();

    [GeneratedRegex(@"[()]|\b(?<word>BETWEEN|AND|OR)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex LogicalToken();

    private const int MaxPasses = 2000;

    public static string JoinShort(string text)
    {
        var pos = 0;
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var masked = LineText.Mask(text);
            var m = Case().Match(masked, pos);
            if (!m.Success) return text;
            pos = m.Index + m.Length;
            if (EndBefore().IsMatch(masked[..m.Index])) continue; // END CASE
            var whens = 0;
            var end = -1;
            for (var t = CaseToken().Match(masked, pos); t.Success; t = t.NextMatch())
            {
                var w = t.Value.ToUpperInvariant();
                if (w is "CASE" or "SELECT" or ";") break;
                if (w is "WHEN")
                {
                    whens++;
                }
                else if (w is "END")
                {
                    end = t.Index + t.Length;
                    break;
                }
            }
            if (end < 0 || whens != 1 || StatementEnd().IsMatch(masked[end..])) continue;
            if (LineText.HasComment(text[m.Index..end]) || !masked[m.Index..end].Contains('\n', StringComparison.Ordinal)) continue;
            var joined = LineText.Squash(text[m.Index..end], masked[m.Index..end]);
            if (joined.Length > OneLine) continue;
            int a = m.Index, b = end;
            var before = OpenerBefore().Match(masked[..a]);
            if (before.Success)
            {
                a = before.Groups["gap"].Index;
                if (!string.Equals(before.Groups["op"].Value, "(", StringComparison.Ordinal)) joined = " " + joined;
            }
            var after = CloserAfter().Match(masked[b..]);
            if (after.Success && before.Success && string.Equals(before.Groups["op"].Value, "(", StringComparison.Ordinal)) b += after.Length - 1;
            // 'text ' || CASE ... END || '.': the CASE is an operand of a || chain, it stays on that line.
            var concatBefore = before.Success ? Match.Empty : ConcatBefore().Match(masked[..a]);
            if (concatBefore.Success)
            {
                a = concatBefore.Groups["gap"].Index;
                joined = " " + joined;
            }
            var concatAfter = ConcatAfter().Match(masked[b..]);
            if (concatAfter.Success && concatBefore.Success)
            {
                text = text[..a] + joined + " " + text[(b + concatAfter.Length)..];
                pos = a + joined.Length;
                continue;
            }
            text = text[..a] + joined + text[b..];
            pos = a + joined.Length;
        }
        return text;
    }

    public static string Relayout(string text, int margin)
    {
        var pos = 0;
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var masked = LineText.Mask(text);
            var m = Case().Match(masked, pos);
            if (!m.Success) return text;
            pos = m.Index + m.Length;
            if (EndBefore().IsMatch(masked[..m.Index])) continue;
            var depth = 0;
            var end = -1;
            var parts = new List<(string Kind, int Start, int End)>();
            var cur = m.Index + m.Length;
            var kind = "SEL";
            var bad = false;
            for (var t = CasePart().Match(masked, cur); t.Success; t = t.NextMatch())
            {
                var w = t.Value.ToUpperInvariant();
                if (w is "SELECT" or "CASE" or ";")
                {
                    bad = true;
                    break;
                }
                if (w is "(")
                {
                    depth++;
                }
                else if (w is ")")
                {
                    depth--;
                    if (depth < 0)
                    {
                        bad = true;
                        break;
                    }
                }
                else if (depth == 0 && w is "WHEN" or "THEN" or "ELSE" or "END")
                {
                    parts.Add((kind, cur, t.Index));
                    kind = w;
                    cur = t.Index + t.Length;
                    if (w is "END")
                    {
                        end = t.Index + t.Length;
                        break;
                    }
                }
            }
            if (bad || end < 0 || !masked[m.Index..end].Contains('\n', StringComparison.Ordinal) || LineText.HasComment(text[m.Index..end])) continue;
            if (parts.Exists(p => p.Kind is "THEN" or "ELSE" && text[p.Start..p.End].Trim().Contains('\n', StringComparison.Ordinal))) continue;
            if (StatementEnd().IsMatch(masked[end..])) continue;
            var lineStart = masked.LastIndexOf('\n', Math.Max(0, m.Index - 1)) + 1;
            if (m.Index == 0) lineStart = 0;
            var c = m.Index - lineStart;
            var selector = Collapse(text[parts[0].Start..parts[0].End], masked[parts[0].Start..parts[0].End]);
            var lines = new List<string> { "CASE" + (selector.Length > 0 ? " " + selector : "") };
            var k = 1;
            while (k < parts.Count)
            {
                var (pk, pa, pb) = parts[k];
                var body = Collapse(text[pa..pb], masked[pa..pb]);
                if (pk is "WHEN" && k + 1 < parts.Count && parts[k + 1].Kind is "THEN")
                {
                    var value = Collapse(text[parts[k + 1].Start..parts[k + 1].End], masked[parts[k + 1].Start..parts[k + 1].End]);
                    var one = new string(' ', c + 2) + "WHEN " + body + " THEN";
                    var cuts = LogicalCuts(LineText.Mask(body));
                    if (one.Length <= margin || cuts.Count == 0)
                    {
                        lines.Add(one[c..]);
                    }
                    else
                    {
                        var bounds = new List<int> { 0 };
                        bounds.AddRange(cuts);
                        bounds.Add(body.Length);
                        var pieces = new List<string>();
                        for (var x = 0; x + 1 < bounds.Count; x++) pieces.Add(body[bounds[x]..bounds[x + 1]].Trim());
                        lines.Add("  WHEN " + pieces[0]);
                        lines.AddRange(pieces.Skip(1).Select(p => "       " + p));
                        lines.Add("  THEN");
                    }
                    lines.Add("    " + value);
                    k += 2;
                }
                else if (pk is "ELSE")
                {
                    lines.Add("  ELSE");
                    lines.Add("    " + body);
                    k++;
                }
                else
                {
                    break;
                }
            }
            if (k < parts.Count) continue;
            var indent = "\n" + new string(' ', c);
            var replacement = string.Join(indent, lines) + indent + "END";
            if (string.Equals(replacement, text[m.Index..end], StringComparison.Ordinal)) continue;
            text = text[..m.Index] + replacement + text[end..];
            pos = m.Index + replacement.Length;
        }
        return text;
    }

    public static string ExpandLong(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = new List<string>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            var c = l.Length > Hard ? OneLineCase().Match(masked[i]) : Match.Empty;
            if (!c.Success || masked[i][(c.Index + 4)..(c.Index + c.Length)].ToUpperInvariant().Replace(" END", "", StringComparison.Ordinal).Contains("CASE", StringComparison.Ordinal))
            {
                output.Add(l);
                continue;
            }
            var col = c.Index;
            var when = c.Groups["when"];
            var other = c.Groups["else"];
            output.Add(l[..col] + "CASE");
            output.Add(new string(' ', col + 2) + l[when.Index..(when.Index + when.Length)]);
            output.Add(new string(' ', col + 2) + l[other.Index..(other.Index + other.Length)]);
            output.Add(new string(' ', col) + l[(c.Index + c.Length - 3)..]);
        }
        return string.Join('\n', output);
    }

    /// <summary>Whitespace runs outside literals to one blank, no blank after <c>(</c> or before <c>)</c>.</summary>
    private static string Collapse(string s, string masked)
    {
        var squashed = LineText.Squash(s, masked);
        var squashedMask = LineText.Squash(masked, masked);
        var sb = new StringBuilder(squashed.Length);
        var sm = new StringBuilder(squashed.Length);
        for (var i = 0; i < squashed.Length && i < squashedMask.Length; i++)
        {
            var mc = squashedMask[i];
            if (mc == ' ' && sm.Length > 0 && sm[^1] == '(') continue;
            if (mc == ')' && sm.Length > 0 && sm[^1] == ' ')
            {
                sb.Length--;
                sm.Length--;
            }
            sb.Append(squashed[i]);
            sm.Append(mc);
        }
        return sb.ToString();
    }

    /// <summary>The positions of the top-level AND / OR of a one-line condition (the AND of BETWEEN is not a cut).</summary>
    private static List<int> LogicalCuts(string masked)
    {
        var depth = 0;
        var cuts = new List<int>();
        var between = false;
        foreach (Match t in LogicalToken().Matches(masked))
        {
            var token = t.Value.ToUpperInvariant();
            if (token[0] == '(')
            {
                depth++;
            }
            else if (token[0] == ')')
            {
                depth--;
            }
            else if (depth == 0)
            {
                if (token is "BETWEEN") between = true;
                else if (token is "AND" && between) between = false;
                else cuts.Add(t.Index);
            }
        }
        return cuts;
    }
}
