using System.Text;
using System.Text.RegularExpressions;
using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>R04 and R06: the case of Oracle built-ins. Only code is looked at (comments and literals are masked).</summary>
internal static partial class CaseRules
{
    /// <summary>Oracle SQL built-in functions that the Beautifier's case rules do not know (lower case here; matched without regard to case).</summary>
    private static readonly HashSet<string> SqlFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "abs", "acos", "add_months", "ascii", "asin", "atan", "atan2", "bin_to_num", "bitand", "ceil", "chr", "coalesce", "concat",
        "convert", "cos", "cosh", "decode", "dump", "exp", "extract", "first_value", "floor", "greatest", "hextoraw", "initcap", "instr",
        "last_day", "lag", "lead", "least", "length", "listagg", "lower", "lpad", "ltrim", "median", "mod", "months_between", "nanvl",
        "next_day", "nullif", "nvl", "nvl2", "power", "rank", "rawtohex", "regexp_instr", "regexp_like", "regexp_replace",
        "regexp_substr", "remainder", "replace", "round", "row_number", "rpad", "rtrim", "sign", "sin", "sinh", "soundex", "sqrt",
        "stddev", "substr", "sys_context", "sys_guid", "tan", "tanh", "to_char", "to_clob", "to_date", "to_nchar", "to_number",
        "to_timestamp", "translate", "trim", "trunc", "tz_offset", "upper", "userenv", "variance",
    };

    [GeneratedRegex(@"(?<![\w$#.])(?<name>[A-Za-z_][A-Za-z_0-9]*)(?=\s*\()", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex FunctionCall();

    [GeneratedRegex(@"%(?:rowtype|type|found|notfound|isopen|rowcount|bulk_rowcount|bulk_exceptions)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Attribute();

    /// <summary>R04: <c>nvl(</c> -> <c>NVL(</c>; a name after a dot (a package's own function) is left alone.</summary>
    public static string UpperBuiltinFunctions(string text)
    {
        var masked = PlsqlLexer.Mask(text);
        var sb = new StringBuilder(text);
        foreach (Match m in FunctionCall().Matches(masked))
        {
            var word = m.Groups["name"];
            if (SqlFunctions.Contains(word.Value)) Upper(sb, word.Index, word.Length);
        }
        return sb.ToString();
    }

    /// <summary>R06: <c>%type</c> -> <c>%TYPE</c>, <c>%rowtype</c>, <c>%found</c>, <c>%notfound</c>, <c>%isopen</c>, <c>%rowcount</c>, <c>%bulk_rowcount</c>, <c>%bulk_exceptions</c>.</summary>
    public static string UpperAttributes(string text)
    {
        var masked = PlsqlLexer.Mask(text);
        var sb = new StringBuilder(text);
        foreach (Match m in Attribute().Matches(masked)) Upper(sb, m.Index, m.Length);
        return sb.ToString();
    }

    private static void Upper(StringBuilder sb, int start, int length)
    {
        for (var i = start; i < start + length; i++) sb[i] = char.ToUpperInvariant(sb[i]);
    }
}
