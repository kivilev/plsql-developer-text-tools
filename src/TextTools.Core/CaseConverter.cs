using System.Text;
using System.Text.RegularExpressions;

namespace TextTools.Core;

/// <summary>
/// Converts every identifier in the text to the target case, leaving the rest untouched:
/// "first_name, last_name" → "firstName, lastName".
/// PL/SQL identifiers may contain $ and #; kebab-case words are joined by single hyphens.
/// </summary>
public static partial class CaseConverter
{
    [GeneratedRegex(@"[\p{L}\p{N}_$#]+(?:-[\p{L}\p{N}_$#]+)*", RegexOptions.ExplicitCapture, Limits.RegexTimeoutMs)]
    private static partial Regex Identifier();

    public static string ToCamel(string text) => Convert(text, w => Join(w, "", first: Lower, rest: Capitalize));
    public static string ToPascal(string text) => Convert(text, w => Join(w, "", first: Capitalize, rest: Capitalize));
    public static string ToSnake(string text) => Convert(text, w => Join(w, "_", first: Lower, rest: Lower));
    public static string ToUpperSnake(string text) => Convert(text, w => Join(w, "_", first: Upper, rest: Upper));
    public static string ToKebab(string text) => Convert(text, w => Join(w, "-", first: Lower, rest: Lower));

    private static string Convert(string text, Func<IReadOnlyList<string>, string> join) =>
        Identifier().Replace(text, m =>
        {
            var words = SplitWords(m.Value);
            return words.Count == 0 ? m.Value : join(words);
        });

    private static string Join(IReadOnlyList<string> words, string sep, Func<string, string> first, Func<string, string> rest) =>
        string.Join(sep, words.Select((w, i) => i == 0 ? first(w) : rest(w)));

    private static string Lower(string w) => w.ToLowerInvariant();
    private static string Upper(string w) => w.ToUpperInvariant();
    private static string Capitalize(string w) => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant();

    /// <summary>"XMLHttpRequest2_id" → [XML, Http, Request2, id]. Separators: _ and -; boundaries: aB, ABc.</summary>
    public static IReadOnlyList<string> SplitWords(string identifier)
    {
        var words = new List<string>();
        foreach (var part in identifier.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries))
        {
            var sb = new StringBuilder();
            for (var i = 0; i < part.Length; i++)
            {
                var c = part[i];
                var boundary = sb.Length > 0 && char.IsUpper(c) &&
                               (char.IsLower(part[i - 1]) || char.IsDigit(part[i - 1]) ||
                                (i + 1 < part.Length && char.IsLower(part[i + 1]) && char.IsUpper(part[i - 1])));
                if (boundary)
                {
                    words.Add(sb.ToString());
                    sb.Clear();
                }
                sb.Append(c);
            }
            if (sb.Length > 0) words.Add(sb.ToString());
        }
        return words;
    }
}
