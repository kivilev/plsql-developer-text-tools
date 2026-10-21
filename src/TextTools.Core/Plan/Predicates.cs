using System.Text;
using System.Text.RegularExpressions;

namespace TextTools.Core.Plan;

/// <summary>A column in a plan predicate: <c>"C"."CLIENT_ID"</c> (alias C) or <c>"CLIENT_ID"</c> (no alias).</summary>
public readonly record struct ColumnRef(string Alias, string Column);

/// <summary>A function applied directly to a column: <c>TO_CHAR("C"."CLIENT_ID")</c>.</summary>
public readonly record struct WrappedColumn(string Function, ColumnRef Column, string Text);

/// <summary>
/// Reads ACCESS_PREDICATES / FILTER_PREDICATES of PLAN_TABLE. The optimizer prints columns double-quoted, binds as
/// :NAME and literals single-quoted, so string literals are blanked first and quoted names cannot be confused with text.
/// </summary>
public static partial class Predicates
{
    private const char Quote = '\'';

    public static IEnumerable<ColumnRef> Columns(string predicate) =>
        ColumnPattern().Matches(WithoutLiterals(predicate)).Select(m => new ColumnRef(m.Groups["alias"].Value, m.Groups["column"].Value));

    /// <summary>Functions whose first argument is a column (not a bind or a literal: those conversions are harmless).</summary>
    public static IEnumerable<WrappedColumn> Wrapped(string predicate)
    {
        var text = WithoutLiterals(predicate);
        foreach (Match m in WrappedPattern().Matches(text))
        {
            yield return new WrappedColumn(m.Groups["fn"].Value.ToUpperInvariant(), new ColumnRef(m.Groups["alias"].Value, m.Groups["column"].Value), Call(text, m.Index));
        }
    }

    /// <summary>
    /// A join condition whose one side is a wrapped column and the other a column of another alias:
    /// <c>TO_NUMBER("B"."CODE")="A"."ID"</c>.
    /// </summary>
    public static bool IsJoinOfDifferentTypes(string predicate, WrappedColumn wrapped)
    {
        foreach (var part in Conjuncts(WithoutLiterals(predicate)))
        {
            if (!part.Contains(wrapped.Text, StringComparison.Ordinal)) continue;
            var rest = part.Replace(wrapped.Text, "", StringComparison.Ordinal);
            if (Columns(rest).Any(c => !c.Alias.Equals(wrapped.Column.Alias, StringComparison.Ordinal))) return true;
        }
        return false;
    }

    /// <summary>Literals replaced by '' (doubled quotes inside them included).</summary>
    public static string WithoutLiterals(string predicate)
    {
        var sb = new StringBuilder(predicate.Length);
        var inString = false;
        var i = 0;
        while (i < predicate.Length)
        {
            var c = predicate[i++];
            if (!inString)
            {
                sb.Append(c);
                inString = c == Quote;
            }
            else if (c == Quote && i < predicate.Length && predicate[i] == Quote)
            {
                i++; // a doubled quote inside the literal
            }
            else if (c == Quote)
            {
                sb.Append(c);
                inString = false;
            }
        }
        return sb.ToString();
    }

    /// <summary>Top-level AND parts (parentheses respected).</summary>
    private static List<string> Conjuncts(string text)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')')
            {
                depth--;
            }
            else if (depth == 0 && string.CompareOrdinal(text, i, " AND ", 0, 5) == 0)
            {
                parts.Add(text[start..i]);
                start = i + 5;
            }
        }
        parts.Add(text[start..]);
        return parts;
    }

    /// <summary>The call starting at <paramref name="start"/> up to its closing parenthesis.</summary>
    private static string Call(string text, int start)
    {
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')' && --depth == 0)
            {
                return text[start..(i + 1)];
            }
        }
        return text[start..];
    }

    [GeneratedRegex(@"(?:""(?<alias>[^""]+)""\.)?""(?<column>[^""]+)""", RegexOptions.ExplicitCapture, Limits.RegexTimeoutMs)]
    private static partial Regex ColumnPattern();

    [GeneratedRegex(@"\b(?<fn>[A-Za-z_][A-Za-z0-9_$#]*)\(\s*(?:""(?<alias>[^""]+)""\.)?""(?<column>[^""]+)""\s*[,)]", RegexOptions.ExplicitCapture, Limits.RegexTimeoutMs)]
    private static partial Regex WrappedPattern();
}
