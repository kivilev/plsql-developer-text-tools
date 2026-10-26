using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>R10, UPDATE ... SET: a bare <c>SET</c> is joined with its first assignment, the <c>=</c> of the assignments are aligned.</summary>
internal static partial class UpdateSetRule
{
    private const string Ident = @"[A-Za-z_][\w$#]*";

    [GeneratedRegex(@"^(?<indent>\s*)SET\s+(?<name>" + Ident + @"(?:\.\s*" + Ident + @")?)\s*=\s*(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex FirstAssignment();

    [GeneratedRegex(@"^(?<indent>\s+)(?<name>" + Ident + @"(?:\.\s*" + Ident + @")?)\s*=\s*(?<rest>.*)$", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex NextAssignment();

    [GeneratedRegex(@"^\s*SET\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex BareSet();

    /// <summary>The <c>=</c> of a group of assignments under one SET in a column.</summary>
    public static string AlignEquals(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = (string[])lines.Clone();
        var i = 0;
        while (i < lines.Length)
        {
            var m = FirstAssignment().Match(masked[i]);
            if (!m.Success || masked[i][..m.Groups["rest"].Index].Contains(';', StringComparison.Ordinal))
            {
                i++;
                continue;
            }
            var column = m.Groups["name"].Index;
            var group = new List<(int Line, string Name, string Tail)> { (i, m.Groups["name"].Value, lines[i][m.Groups["rest"].Index..]) };
            var j = i;
            while (masked[j].TrimEnd().EndsWith(',') && j + 1 < lines.Length)
            {
                var n = NextAssignment().Match(masked[j + 1]);
                if (!n.Success || n.Groups["indent"].Length != column) break;
                j++;
                group.Add((j, n.Groups["name"].Value, lines[j][n.Groups["rest"].Index..]));
            }
            if (group.Count >= 2)
            {
                var width = group.Max(g => g.Name.Length);
                for (var k = 0; k < group.Count; k++)
                {
                    var (line, name, tail) = group[k];
                    var prefix = k > 0 ? lines[line][..column] : lines[line][..m.Groups["name"].Index];
                    output[line] = prefix + name.PadRight(width) + " = " + tail;
                }
            }
            i = j + 1;
        }
        return string.Join('\n', output);
    }

    /// <summary><c>SET</c> alone on its line: the first assignment goes back next to it.</summary>
    public static string JoinBareSet(string text)
    {
        var lines = text.Split('\n');
        var output = new List<string>(lines.Length);
        var i = 0;
        while (i < lines.Length)
        {
            if (BareSet().IsMatch(lines[i]) && i + 1 < lines.Length && lines[i + 1].Trim().Length > 0 && !LineText.IsCommentOnly(lines[i + 1]))
            {
                output.Add(lines[i].TrimEnd() + " " + lines[i + 1].Trim());
                i += 2;
                continue;
            }
            output.Add(lines[i]);
            i++;
        }
        return string.Join('\n', output);
    }
}
