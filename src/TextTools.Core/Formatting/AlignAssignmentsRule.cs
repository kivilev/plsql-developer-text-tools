using System.Text.RegularExpressions;
using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>R08: <c>:=</c> aligned in a group of consecutive assignments of the same indent; a single assignment gets one space on each side.</summary>
internal static partial class AlignAssignmentsRule
{
    [GeneratedRegex(@"^(?<indent>\s*)(?<name>[A-Za-z_][\w$#.()]*)\s*:=\s*(?<rest>.*)$", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Assignment();

    public static string Apply(string text)
    {
        var lines = text.Split('\n');
        var masked = PlsqlLexer.Mask(text).Split('\n');
        var output = (string[])lines.Clone();
        var i = 0;
        while (i < lines.Length)
        {
            var first = Assignment().Match(masked[i]);
            if (!first.Success)
            {
                i++;
                continue;
            }
            var indent = first.Groups["indent"].Length;
            var j = i;
            var group = new List<(int Line, Match Match)>();
            while (j < lines.Length)
            {
                var m = Assignment().Match(masked[j]);
                if (!m.Success || m.Groups["indent"].Length != indent) break;
                group.Add((j, m));
                if (!masked[j].TrimEnd().EndsWith(';'))
                {
                    j++;
                    break; // the assignment goes on in the next line: the group ends with it
                }
                j++;
            }
            if (group.Count > 0)
            {
                var width = group.Count >= 2 ? group.Max(g => g.Match.Groups["name"].Length) : 0;
                foreach (var (line, m) in group)
                {
                    output[line] = new string(' ', indent) + m.Groups["name"].Value.PadRight(width) + " := " + lines[line][m.Groups["rest"].Index..];
                }
            }
            i = Math.Max(j, i + 1);
        }
        return string.Join('\n', output);
    }
}
