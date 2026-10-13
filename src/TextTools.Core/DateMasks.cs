using System.Globalization;
using System.Text;

namespace TextTools.Core;

/// <summary>Converts date format masks between Java (DateTimeFormatter / SimpleDateFormat), .NET and Oracle.</summary>
public static class DateMasks
{
    private const string Punctuation = " -/,.;:";

    /// <summary>yyyy-MM-dd'T'HH:mm:ss.SSSXXX → YYYY-MM-DD"T"HH24:MI:SS.FF3TZH:TZM. A "..."-wrapped string becomes '...'.</summary>
    public static string ToOracle(string text)
    {
        var (mask, wrapped) = Unwrap(text.Trim(), '"');
        var sb = new StringBuilder();
        for (var i = 0; i < mask.Length;)
        {
            var c = mask[i];
            if (c == '\'')
            {
                var end = mask.IndexOf('\'', i + 1);
                if (end < 0) throw new FormatException("unclosed quote in mask");
                var literal = mask[(i + 1)..end];
                sb.Append(literal.Length == 0 ? "'" : $"\"{literal}\"");
                i = end + 1;
                continue;
            }
            if (!char.IsAsciiLetter(c))
            {
                sb.Append(Punctuation.Contains(c) ? c.ToString() : $"\"{c}\"");
                i++;
                continue;
            }
            var n = 1;
            while (i + n < mask.Length && mask[i + n] == c) n++;
            sb.Append(JavaLetter(c, n));
            i += n;
        }
        return wrapped ? $"'{sb}'" : sb.ToString();
    }

    /// <summary>Oracle element for a run of <paramref name="n"/> identical Java/.NET pattern letters.</summary>
    private static string JavaLetter(char c, int n) => (c, n) switch
    {
        ('y' or 'u', 2) => "YY",
        ('y' or 'u', _) => "YYYY",
        ('M' or 'L', >= 4) => "FMMonth",
        ('M' or 'L', 3) => "Mon",
        ('M' or 'L', _) => "MM",
        ('d', >= 4) => "FMDay",                 // .NET dddd / ddd are day names
        ('d', 3) => "Dy",
        ('d', _) => "DD",
        ('D', _) => "DDD",
        ('E', >= 4) => "FMDay",
        ('E', _) => "Dy",
        ('H' or 'k', _) => "HH24",
        ('h', _) => "HH12",
        ('m', _) => "MI",
        ('s', _) => "SS",
        ('S' or 'f' or 'F', _) => "FF" + Math.Min(n, 9).ToString(CultureInfo.InvariantCulture),
        ('a' or 't', _) => "AM",
        ('X' or 'x', 1) => "TZH",
        ('X' or 'x', 2) => "TZHTZM",
        ('X' or 'x', _) => "TZH:TZM",
        ('Z', >= 4) => "TZH:TZM",
        ('Z', _) => "TZHTZM",
        ('z', >= 3) => "TZH:TZM",               // .NET zzz = +03:00, Java z = zone name
        ('z', 2) => "TZH",
        ('z', _) => "TZR",
        ('K', _) => "TZH:TZM",
        ('V', _) => "TZR",
        ('G', _) => "AD",
        _ => throw new FormatException($"unsupported pattern letter '{c}'"),
    };

    public static string ToJava(string text) => FromOracle(text, java: true);
    public static string ToDotNet(string text) => FromOracle(text, java: false);

    // Longest tokens first. Value: (Java, .NET); null = no equivalent.
    private static readonly (string Token, string? Java, string? Net)[] OracleTokens =
    [
        ("TZH:TZM", "XXX", "zzz"), ("SSSSS", null, null), ("SYYYY", "yyyy", "yyyy"), ("MONTH", "MMMM", "MMMM"),
        ("HH24", "HH", "HH"), ("HH12", "hh", "hh"), ("YYYY", "yyyy", "yyyy"), ("RRRR", "yyyy", "yyyy"),
        ("A.M.", "a", "tt"), ("P.M.", "a", "tt"),
        ("FF1", "S", "f"), ("FF2", "SS", "ff"), ("FF3", "SSS", "fff"), ("FF4", "SSSS", "ffff"), ("FF5", "SSSSS", "fffff"),
        ("FF6", "SSSSSS", "ffffff"), ("FF7", "SSSSSSS", "fffffff"), ("FF8", "SSSSSSSS", null), ("FF9", "SSSSSSSSS", null),
        ("DAY", "EEEE", "dddd"), ("MON", "MMM", "MMM"), ("DDD", "D", null), ("TZH", "X", "zz"), ("TZR", "VV", null),
        ("YY", "yy", "yy"), ("RR", "yy", "yy"), ("MM", "MM", "MM"), ("DY", "EEE", "ddd"), ("DD", "dd", "dd"),
        ("HH", "hh", "hh"), ("MI", "mm", "mm"), ("SS", "ss", "ss"), ("FF", "SSSSSS", "ffffff"),
        ("AM", "a", "tt"), ("PM", "a", "tt"), ("FM", "", ""), ("FX", "", ""),
    ];

    /// <summary>YYYY-MM-DD HH24:MI:SS → yyyy-MM-dd HH:mm:ss. A '...'-wrapped Oracle string becomes "...".</summary>
    private static string FromOracle(string text, bool java)
    {
        var (mask, wrapped) = Unwrap(text.Trim(), '\'');
        var sb = new StringBuilder();
        for (var i = 0; i < mask.Length;)
        {
            var c = mask[i];
            if (c == '"')
            {
                var end = mask.IndexOf('"', i + 1);
                if (end < 0) throw new FormatException("unclosed quote in mask");
                sb.Append('\'').Append(mask[(i + 1)..end].Replace("'", "''")).Append('\'');
                i = end + 1;
                continue;
            }
            if (!char.IsAsciiLetter(c))
            {
                sb.Append(c);
                i++;
                continue;
            }
            var token = OracleTokens.FirstOrDefault(t =>
                string.Compare(mask, i, t.Token, 0, t.Token.Length, StringComparison.OrdinalIgnoreCase) == 0);
            if (token.Token is null) throw new FormatException($"unsupported Oracle mask element at '{mask[i..]}'");
            sb.Append((java ? token.Java : token.Net)
                      ?? throw new FormatException($"{token.Token} has no {(java ? "Java" : ".NET")} equivalent"));
            i += token.Token.Length;
        }
        return wrapped ? $"\"{sb}\"" : sb.ToString();
    }

    private static (string Mask, bool Wrapped) Unwrap(string s, char quote) =>
        s.Length >= 2 && s[0] == quote && s[^1] == quote ? (s[1..^1], true) : (s, false);
}
