using System.Text.RegularExpressions;

namespace TextTools.Core.Formatting;

/// <summary>Declarations (<c>v_name  NUMBER;</c>) of a declaration section aligned in groups of the same indent: the types start in one column.</summary>
internal static partial class AlignDeclarationsRule
{
    private static readonly HashSet<string> NotDeclarations = new(StringComparer.Ordinal)
    {
        "PROCEDURE", "FUNCTION", "TYPE", "SUBTYPE", "CURSOR", "PRAGMA", "RETURN", "IF", "ELSIF", "ELSE", "END", "BEGIN",
        "EXCEPTION", "WHEN", "NULL", "RAISE", "OPEN", "CLOSE", "FETCH", "COMMIT", "ROLLBACK", "SAVEPOINT", "LOOP", "FOR",
        "WHILE", "EXIT", "CONTINUE", "CASE", "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "EXECUTE", "GOTO", "FORALL",
    };

    [GeneratedRegex(@"^(?<indent>\s*)(?<name>[A-Za-z_][\w$#]*)\s+(?<rest>.*;)\s*$", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Declaration();

    [GeneratedRegex(@"^(?:CREATE\s+OR\s+REPLACE\s+)?(?:PACKAGE|PROCEDURE|FUNCTION|TYPE\s+BODY|TRIGGER)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Header();

    [GeneratedRegex(@"\b(?:IS|AS)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex EndsWithIsAs();

    [GeneratedRegex(@"^(?:BEGIN|END)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex BeginOrEnd();

    private sealed record Member(int Line, string Indent, string Name, string Rest);

    public static string Apply(string text)
    {
        var (lines, masked) = LineText.Split(text);
        var output = (string[])lines.Clone();
        var inDeclarations = false;
        var headerPending = false;
        var group = new List<Member>();

        void Flush()
        {
            if (group.Count >= 2)
            {
                var width = group.Max(g => g.Name.Length);
                foreach (var g in group) output[g.Line] = g.Indent + g.Name.PadRight(width) + " " + g.Rest;
            }
            group.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var st = masked[i].Trim();
            if (Header().IsMatch(st) || headerPending)
            {
                if (EndsWithIsAs().IsMatch(st))
                {
                    inDeclarations = true;
                    headerPending = false;
                }
                else
                {
                    headerPending = !st.EndsWith(';');
                }
                Flush();
                continue;
            }
            if (BeginOrEnd().IsMatch(st) || st.Equals("DECLARE", StringComparison.OrdinalIgnoreCase))
            {
                inDeclarations = st.Equals("DECLARE", StringComparison.OrdinalIgnoreCase);
                Flush();
                continue;
            }
            var d = inDeclarations ? Declaration().Match(masked[i]) : null;
            if (d is { Success: true } && !NotDeclarations.Contains(d.Groups["name"].Value.ToUpperInvariant()) && !st.StartsWith("--", StringComparison.Ordinal))
            {
                var indent = d.Groups["indent"].Value;
                if (group.Count > 0 && !string.Equals(group[^1].Indent, indent, StringComparison.Ordinal)) Flush();
                var nameEnd = d.Groups["name"].Index + d.Groups["name"].Length;
                group.Add(new Member(i, indent, d.Groups["name"].Value, lines[i][nameEnd..].TrimStart()));
            }
            else
            {
                Flush();
            }
        }
        Flush();
        return string.Join('\n', output);
    }
}
