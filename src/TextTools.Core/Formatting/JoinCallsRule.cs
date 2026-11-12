using System.Text.RegularExpressions;
using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>
/// A statement that the Beautifier broke over several lines but that fits the margin in one line (the closing semicolon may stand
/// past it) goes back into one line: <c>call(a =&gt; x, b =&gt; y);</c>, <c>v := f(a, b);</c>. Only plain call and assignment statements
/// are joined; queries, conditions, comments and string literals that span lines are left alone.
/// </summary>
internal static partial class JoinCallsRule
{
    private const int MaxLines = 12;

    /// <summary>The width that joins a whole statement, whatever its length (the wrap rule breaks it again).</summary>
    private const int Unlimited = 2000;

    private const int MaxLinesUnlimited = 300;

    [GeneratedRegex(@"^\s*(?:RETURN\s+|[A-Za-z_][\w$#.()]*(?:\s+[A-Za-z_][\w$#.%()]*)?\s*:=\s*)\S", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex ExpressionStart();

    [GeneratedRegex(@"\b(?:CASE|SELECT|CURSOR|IS|AS|INTO|BULK|FROM|WHERE|USING|RETURNING|THEN|LOOP|BEGIN|DECLARE|END)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex NotAnExpression();

    [GeneratedRegex(@"(?:;|\b(?:BEGIN|THEN|ELSE|LOOP|IS|AS|DECLARE|EXCEPTION))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex StatementBoundary();

    /// <summary>True when the code line before <paramref name="line"/> ends a statement or opens a block: the line starts a statement.</summary>
    private static bool StartsAStatement(string[] masked, int line)
    {
        for (var k = line - 1; k >= 0; k--)
        {
            var code = masked[k].Trim();
            if (code.Length > 0) return StatementBoundary().IsMatch(code);
        }
        return true;
    }

    [GeneratedRegex(@"^\s*(?:[A-Za-z_][\w$#.()]*\s*:=\s*)?[A-Za-z_][\w$#]*(?:\.[A-Za-z_][\w$#]*)*\s*\(", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex CallStart();

    [GeneratedRegex(@"^\s*(?:SELECT|INSERT|UPDATE|DELETE|MERGE|IF|ELSIF|ELSE|WHEN|OPEN|FOR|WHILE|RETURN|CASE|LOOP|EXECUTE|FETCH|CLOSE|EXIT|RAISE|WITH|USING|INTO|VALUES|SET|FROM|WHERE|AND|OR|ON|JOIN|CURSOR|TYPE|PROCEDURE|FUNCTION|PRAGMA|BEGIN|DECLARE|END|EXCEPTION)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex NotAStatement();

    public static string Apply(string text, int margin, bool wholeStatements = false)
    {
        var (lines, masked) = LineText.Split(text);
        var insideLiteral = LinesInsideLiterals(text);
        var output = new List<string>(lines.Length);
        var i = 0;
        while (i < lines.Length)
        {
            if (TryJoin(lines, masked, insideLiteral, i, margin, wholeStatements, out var joined, out var count))
            {
                output.Add(joined);
                i += count;
            }
            else
            {
                output.Add(lines[i]);
                i++;
            }
        }
        return string.Join('\n', output);
    }

    private static bool TryJoin(string[] lines, string[] masked, HashSet<int> insideLiteral, int start, int margin, bool wholeStatements, out string joined, out int count)
    {
        joined = "";
        count = 0;
        var first = masked[start];
        if (insideLiteral.Contains(start) || first.TrimEnd().EndsWith(';')) return false;
        var call = CallStart().IsMatch(first) && !NotAStatement().IsMatch(first);
        // With short arguments kept up to 120 a whole statement is joined, whatever its length: a call, RETURN f(...), x := ... and
        // t T := f(...), but only at the start of a statement (a call inside a SELECT list keeps the margin).
        var expression = wholeStatements && (call || ExpressionStart().IsMatch(first)) && StartsAStatement(masked, start);
        var limit = expression ? Unlimited : margin;
        if (!call && !expression) return false;

        var depth = 0;
        var end = -1;
        var maxLines = expression ? MaxLinesUnlimited : MaxLines;
        for (var k = start; k < lines.Length && k - start < maxLines; k++)
        {
            if (LineText.HasComment(lines[k]) || insideLiteral.Contains(k) || lines[k].Trim().Length == 0) return false;
            if (expression && NotAnExpression().IsMatch(masked[k])) return false;
            depth += LineText.Net(masked[k]);
            if (depth < 0) return false;
            if (depth == 0 && masked[k].TrimEnd().EndsWith(';'))
            {
                end = k;
                break;
            }
            if (depth == 0 && !expression) return false; // a statement that does not end with ; after the parentheses closed: not a plain call
        }
        if (end <= start) return false;

        var text = new System.Text.StringBuilder(lines[start].TrimEnd());
        for (var k = start + 1; k <= end; k++)
        {
            var part = lines[k].Trim();
            var previousOpens = text[^1] == '(';
            var nextCloses = part.StartsWith(')');
            if (!previousOpens && !nextCloses) text.Append(' ');
            text.Append(part);
        }
        var line = text.ToString();
        var squashed = LineText.Squash(line, LineText.Mask(line));
        squashed = new string(' ', LineText.Indent(lines[start])) + squashed;
        if (squashed.Length - 1 > limit) return false; // the semicolon may stand past the margin
        joined = squashed;
        count = end - start + 1;
        return true;
    }

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
}
