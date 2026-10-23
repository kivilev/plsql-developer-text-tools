using System.Text.RegularExpressions;
using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>R07: the <c>END;</c> that closes a named PACKAGE, PACKAGE BODY, PROCEDURE, FUNCTION, TRIGGER or TYPE BODY gets the name: <c>END open_account;</c>.</summary>
internal static partial class EndNameRule
{
    private const string Ident = @"[A-Za-z_][\w$#]*";

    [GeneratedRegex(@"^(?<indent>\s*)(?:CREATE\s+OR\s+REPLACE\s+)?(?:PACKAGE\s+BODY|PACKAGE|PROCEDURE|FUNCTION|TRIGGER|TYPE\s+BODY)\s+(?<name>" + Ident + @"(?:\.\s*" + Ident + @")?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Header();

    [GeneratedRegex(@"\b(?:IS|AS)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex IsOrAs();

    [GeneratedRegex(@"^(?<indent>\s*)END\s*;\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex PlainEnd();

    [GeneratedRegex(@"^\s*END\s*;(?<tail>.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex EndTail();

    /// <summary>How many lines after a header are searched for its IS/AS (a long parameter list comes first).</summary>
    private const int HeaderLines = 12;

    public static string Apply(string text)
    {
        var lines = text.Split('\n');
        var masked = PlsqlLexer.Mask(text).Split('\n');
        var output = (string[])lines.Clone();
        var open = new Stack<(int Indent, string Name)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var h = Header().Match(masked[i]);
            if (h.Success && !masked[i].TrimEnd().EndsWith(';'))
            {
                var blob = string.Join(' ', masked.Skip(i).Take(HeaderLines));
                var semicolon = blob.IndexOf(';', StringComparison.Ordinal);
                var head = semicolon < 0 ? blob : blob[..semicolon];
                if (IsOrAs().IsMatch(head)) open.Push((h.Groups["indent"].Length, h.Groups["name"].Value.Split('.')[^1].Trim()));
                continue;
            }
            var e = PlainEnd().Match(masked[i]);
            if (e.Success && open.Count > 0 && e.Groups["indent"].Length == open.Peek().Indent)
            {
                output[i] = e.Groups["indent"].Value + "END " + open.Pop().Name + ";" + EndTail().Match(lines[i]).Groups["tail"].Value;
            }
        }
        return string.Join('\n', output);
    }
}
