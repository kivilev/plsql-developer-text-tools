using System.Text.RegularExpressions;
using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>
/// R02 and the layout of a long line: a line past the margin is broken at the outermost call or condition that covers the margin, one
/// argument per line under the first; the <c>=&gt;</c> of named arguments are aligned (R09), a long DECODE breaks in pairs
/// "condition, value", IF / ELSIF conditions break after AND / OR, <c>BETWEEN a AND b</c> breaks before AND, a <c>||</c> chain is
/// filled up to the margin. A line past 120 is wrapped once more with that limit. Lines that start inside a multi-line string
/// literal are never touched.
/// </summary>
internal static partial class WrapRule
{
    private const int Hard = 120;
    private const string Ident = @"[A-Za-z_][\w$#]*";

    [GeneratedRegex(@"^\s*(?:IF|ELSIF)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex IfStart();

    [GeneratedRegex(@"^(?<indent>\s*)(?:IF|ELSIF)\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex IfPrefix();

    [GeneratedRegex(@"[()]|\b(?<word>BETWEEN|AND|OR)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex LogicalToken();

    [GeneratedRegex(@"[()]|\b(?<word>AND|OR)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex AndOrToken();

    [GeneratedRegex(@"[()]|\bAND\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex AndToken();

    [GeneratedRegex(@"\bBETWEEN\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Between();

    [GeneratedRegex(@"(?<name>[\w$#]+)\s*$", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex WordBeforeParenthesis();

    [GeneratedRegex(@"^(?<name>" + Ident + @")\s*=>\s*(?<value>.*)$", RegexOptions.Singleline | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex NamedArgument();

    [GeneratedRegex(@"^(?<lead>\s*(?:[A-Za-z_][\w$#.()]*\s*:=\s*|" + Ident + @"\s*=>\s*|(?:RETURN|IF|ELSIF|WHEN)\s+)?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ConcatLead();

    /// <summary>What the pass may do (the settings behind it).</summary>
    internal readonly record struct Choices(int Margin, bool DecodeInPairs, bool BreakBetween, bool AlignNamedArguments);

    public static string Apply(string text, Choices choices)
    {
        var (lines, masked) = LineText.Split(text);
        var inside = LinesInsideLiterals(text);
        var output = new List<string>(lines.Length + 16);
        var depth = 0; // open parentheses before the line: a line inside an unclosed call is a continuation, cut at 120 only
        for (var n = 0; n < lines.Length; n++)
        {
            var startsInside = depth > 0;
            foreach (var c in masked[n])
            {
                if (c == '(') depth++;
                else if (c == ')' && depth > 0) depth--;
            }
            if (inside.Contains(n))
            {
                output.Add(lines[n]);
                continue;
            }
            var pieces = WrapLine(lines[n], masked[n], startsInside ? Recur(choices.Margin) : choices.Margin, true, choices);
            // When the cut at 120 does not get the line under 120, it is cut at the margin as usual.
            if (startsInside && pieces.Any(r => r.TrimEnd().Length > Hard)) pieces = WrapLine(lines[n], masked[n], choices.Margin, true, choices);
            foreach (var r in pieces)
            {
                if (r.TrimEnd().Length > Hard) output.AddRange(WrapLine(r, LineText.Mask(r), Hard, true, choices));
                else output.Add(r);
            }
        }
        return string.Join('\n', output);
    }

    /// <summary>The 0-based lines that begin inside a string literal that started on an earlier line.</summary>
    private static HashSet<int> LinesInsideLiterals(string text)
    {
        var inside = new HashSet<int>();
        var lineStarts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') lineStarts.Add(i + 1);
        }
        int LineOf(int position)
        {
            var index = lineStarts.BinarySearch(position);
            return index >= 0 ? index : ~index - 1;
        }
        foreach (var t in PlsqlLexer.Tokenize(text))
        {
            if (t.Kind != TokenKind.StringLiteral) continue;
            for (var line = LineOf(t.Start) + 1; line <= LineOf(Math.Max(t.Start, t.End - 1)); line++) inside.Add(line);
        }
        return inside;
    }

    /// <summary>The margin for the lines that wrapping itself makes out of a call or a chain: they are cut again only past the hard limit.</summary>
    private static int Recur(int margin) => Math.Max(margin, Hard);

    private static List<string> Expand(IEnumerable<string> parts, string line, int margin, bool splitters, Choices choices, bool onlyLong)
    {
        var result = new List<string>();
        foreach (var o in parts)
        {
            if (!string.Equals(o, line, StringComparison.Ordinal) && (!onlyLong || o.TrimEnd().Length > margin)) result.AddRange(WrapLine(o, LineText.Mask(o), margin, splitters, choices));
            else result.Add(o);
        }
        return result;
    }

    private static List<string> WrapLine(string line, string masked, int margin, bool splitters, Choices choices)
    {
        var effective = line.TrimEnd().Length;
        if (line.Contains('\t', StringComparison.Ordinal)) return [line];
        // A line that only its closing semicolon pushes past the margin stays as it is (not at the hard limit).
        if (margin != Hard && effective == margin + 1 && masked.TrimEnd().EndsWith(';')) return [line];

        var stack = new Stack<int>();
        var pairs = new List<(int Open, int Close)>();
        for (var i = 0; i < masked.Length; i++)
        {
            if (masked[i] == '(')
            {
                stack.Push(i);
            }
            else if (masked[i] == ')' && stack.Count > 0)
            {
                pairs.Add((stack.Pop(), i));
            }
        }

        if (splitters && effective > margin && IfStart().IsMatch(masked) && WrapLogical(line, masked, margin) is { } logical)
        {
            return Expand(logical, line, margin, false, choices, onlyLong: true);
        }
        if (splitters && effective > margin && choices.BreakBetween && WrapBetween(line, masked, margin) is { } between)
        {
            return Expand(between, line, margin, false, choices, onlyLong: true);
        }

        // A chain of || is broken at the operator before any call in it is split argument by argument:
        // SUBSTR(SQLERRM, 1, 150) must stay in one piece (code standard, section 5).
        // A line that only the closing semicolon pushes past the margin stays as it is.
        if (splitters && effective > margin && HasTopLevelConcat(masked))
        {
            return WrapConcat(line, masked, margin) is { } concat ? Expand(concat, line, Recur(margin), false, choices, onlyLong: true) : [line];
        }

        var candidates = new List<(int Open, int Close, string Separator)>();
        foreach (var (open, close) in pairs.OrderBy(p => p.Open))
        {
            if (candidates.Any(c => c.Open < open && close < c.Close)) continue;
            var isCall = open > 0 && PlsqlLexer.IsIdentifierPart(masked[open - 1]);
            if (isCall) candidates.Add((open, close, ","));
            else if (LogicalCuts(masked, open + 1, close).Count > 0) candidates.Add((open, close, ""));
        }
        foreach (var (open, close, separator) in candidates)
        {
            var cuts = separator.Length > 0 ? LineText.SplitTop(masked, open + 1, close) : LogicalCuts(masked, open + 1, close);
            if (cuts.Count == 0) continue;
            var first = open + 1;
            while (first < close && line[first] == ' ') first++;
            var parts = new List<string>();
            var previous = first;
            foreach (var c in cuts)
            {
                parts.Add(line[previous..c].Trim());
                previous = c + separator.Length;
            }
            parts.Add(line[previous..close].Trim());
            if (effective <= margin || !(open < margin && close >= margin - 1)) continue;

            var head = line[..first];
            var tail = line[close..];
            var column = first;
            var function = WordBeforeParenthesis().Match(masked[..open]);
            if (choices.DecodeInPairs && separator.Length > 0 && function.Success
                && function.Groups["name"].Value.Equals("DECODE", StringComparison.OrdinalIgnoreCase) && parts.Count >= 3)
            {
                parts = DecodePairs(parts, column, margin);
            }
            if (choices.AlignNamedArguments && separator.Length > 0)
            {
                var named = parts.Select(p => NamedArgument().Match(p)).ToList();
                if (named.All(m => m.Success))
                {
                    var width = named.Max(m => m.Groups["name"].Length);
                    parts = named.Select(m => m.Groups["name"].Value.PadRight(width) + " => " + m.Groups["value"].Value).ToList();
                }
            }
            var broken = new List<string>();
            for (var k = 0; k < parts.Count; k++)
            {
                var last = k == parts.Count - 1;
                var piece = parts[k] + (last ? "" : separator) + (last ? tail : "");
                broken.Add((k == 0 ? head : new string(' ', column)) + piece);
            }
            return Expand(broken, line, Recur(margin), true, choices, onlyLong: false);
        }

        if (effective <= margin) return [line];
        if (splitters && WrapLogical(line, masked, margin) is { } wrappedLogical)
        {
            return Expand(wrappedLogical, line, margin, false, choices, onlyLong: true);
        }
        if (splitters && WrapConcat(line, masked, margin) is { } wrappedConcat)
        {
            return Expand(wrappedConcat, line, Recur(margin), false, choices, onlyLong: true);
        }
        return [line];
    }

    /// <summary>DECODE(expr, c1, v1, c2, v2, default): the expression, then pairs "condition, value" on one line when they fit, else each on its own.</summary>
    private static List<string> DecodePairs(List<string> parts, int column, int margin)
    {
        var merged = new List<string> { parts[0] };
        var rest = parts.Skip(1).ToList();
        while (rest.Count >= 2)
        {
            var pair = rest[0] + ", " + rest[1];
            if (column + pair.Length + 1 <= margin) merged.Add(pair);
            else merged.AddRange(rest.Take(2));
            rest = rest.Skip(2).ToList();
        }
        merged.AddRange(rest);
        return merged;
    }

    /// <summary>Positions just after each AND / OR at depth 0 in <c>masked[start..end)</c> (the AND of BETWEEN is not a cut).</summary>
    private static List<int> LogicalCuts(string masked, int start, int end)
    {
        var depth = 0;
        var cuts = new List<int>();
        var between = false;
        foreach (Match m in LogicalToken().Matches(masked[start..end]))
        {
            var token = m.Value.ToUpperInvariant();
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
                if (string.Equals(token, "BETWEEN", StringComparison.Ordinal)) between = true;
                else if (string.Equals(token, "AND", StringComparison.Ordinal) && between) between = false;
                else cuts.Add(start + m.Index + m.Length);
            }
        }
        return cuts;
    }

    /// <summary>... x BETWEEN a AND b: the line breaks before the AND, which is right-aligned under BETWEEN.</summary>
    private static List<string>? WrapBetween(string line, string masked, int margin)
    {
        var m = Between().Match(masked);
        if (!m.Success) return null;
        var depth = 0;
        int? cut = null;
        var after = masked[(m.Index + m.Length)..];
        foreach (Match t in AndToken().Matches(after))
        {
            if (t.Value[0] == '(')
            {
                depth++;
            }
            else if (t.Value[0] == ')')
            {
                depth--;
            }
            else if (depth == 0)
            {
                cut = m.Index + m.Length + t.Index;
                break;
            }
        }
        if (cut is null) return null;
        var first = line[..cut.Value].TrimEnd();
        var second = line[cut.Value..].Trim();
        if (first.Length > margin) return null;
        return [first, new string(' ', m.Index + 4) + second];
    }

    /// <summary>IF a AND b ... THEN: a greedy break after top-level AND / OR, the continuation under the condition.</summary>
    private static List<string>? WrapLogical(string line, string masked, int margin)
    {
        var prefix = IfPrefix().Match(masked);
        if (!prefix.Success) return null;
        var start = prefix.Length;
        var depth = 0;
        var cuts = new List<int>();
        foreach (Match t in AndOrToken().Matches(masked[start..]))
        {
            if (t.Value[0] == '(')
            {
                depth++;
            }
            else if (t.Value[0] == ')')
            {
                depth--;
            }
            else if (depth == 0)
            {
                cuts.Add(start + t.Index + t.Length);
            }
        }
        if (cuts.Count == 0) return null;
        var pieces = new List<string>();
        var previous = start;
        foreach (var c in cuts)
        {
            pieces.Add(line[previous..c].Trim());
            previous = c;
        }
        pieces.Add(line[previous..].Trim());
        var current = line[..start].TrimEnd() + " " + pieces[0];
        var output = new List<string>();
        foreach (var p in pieces.Skip(1))
        {
            var candidate = current + " " + p;
            if (candidate.Length > margin)
            {
                output.Add(current);
                current = new string(' ', start) + p;
            }
            else
            {
                current = candidate;
            }
        }
        output.Add(current);
        return output.Count > 1 ? output : null;
    }

    /// <summary>A top-level <c>||</c> chain filled up to the margin: the break after <c>||</c>, the continuation under the first operand.</summary>
    private static bool HasTopLevelConcat(string masked)
    {
        var depth = 0;
        for (var i = 0; i < masked.Length; i++)
        {
            if (masked[i] == '(') depth++;
            else if (masked[i] == ')') depth--;
            else if (masked[i] == '|' && depth == 0 && i + 1 < masked.Length && masked[i + 1] == '|') return true;
        }
        return false;
    }

    private static List<string>? WrapConcat(string line, string masked, int margin)
    {
        var depth = 0;
        var cuts = new List<int>();
        for (var i = 0; i < masked.Length; i++)
        {
            if (masked[i] == '(') depth++;
            else if (masked[i] == ')') depth--;
            else if (masked[i] == '|' && depth == 0 && i + 1 < masked.Length && masked[i + 1] == '|') cuts.Add(i);
        }
        if (cuts.Count == 0) return null;
        var lead = ConcatLead().Match(masked);
        var start = lead.Success ? lead.Groups["lead"].Length : LineText.Indent(line);
        if (start >= line.Length || cuts[0] < start) return null;
        var pieces = new List<string>();
        var previous = start;
        foreach (var c in cuts)
        {
            pieces.Add(line[previous..c].Trim());
            previous = c + 2;
        }
        pieces.Add(line[previous..].Trim());
        // A line that ends with || continues in the next line: no empty last piece (the reference rule made a blank line of it).
        var endsWithOperator = pieces[^1].Length == 0;
        if (endsWithOperator) pieces.RemoveAt(pieces.Count - 1);
        if (pieces.Count == 0) return null;
        var output = new List<string>();
        var current = line[..start];
        var firstPiece = true;
        for (var k = 0; k < pieces.Count; k++)
        {
            var last = k == pieces.Count - 1;
            var add = pieces[k] + (last && !endsWithOperator ? "" : " ||");
            var candidate = current + (firstPiece ? "" : " ") + add;
            if (!firstPiece && candidate.TrimEnd().TrimEnd(';').Length > margin)
            {
                output.Add(current);
                current = new string(' ', start) + add;
            }
            else
            {
                current = candidate;
            }
            firstPiece = false;
        }
        output.Add(current);
        return output.Count > 1 ? output : null;
    }
}
