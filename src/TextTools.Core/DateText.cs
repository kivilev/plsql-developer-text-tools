using System.Globalization;
using System.Text.RegularExpressions;

namespace TextTools.Core;

/// <summary>A date parsed from the editor: wall-clock value, optional offset and the fraction digits as typed.</summary>
public readonly record struct ParsedDate(DateTime Value, TimeSpan? Offset, bool HasTime, string Fraction);

/// <summary>Oracle date/time literals: "now" values and conversions of a selected date (one per line).</summary>
public static partial class DateText
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ---- Now ----

    public static string NowDate(DateTimeOffset m) => $"DATE '{m.ToString("yyyy-MM-dd", Inv)}'";
    public static string NowTimestamp(DateTimeOffset m) => $"TIMESTAMP '{m.ToString("yyyy-MM-dd HH:mm:ss", Inv)}'";
    public static string NowTimestampFraction(DateTimeOffset m) => $"TIMESTAMP '{m.ToString("yyyy-MM-dd HH:mm:ss.ffffff", Inv)}'";
    public static string NowTimestampTz(DateTimeOffset m) => $"TIMESTAMP '{m.ToString("yyyy-MM-dd HH:mm:ss.ffffff zzz", Inv)}'";
    public static string NowToDate(DateTimeOffset m) =>
        $"TO_DATE('{m.ToString("yyyy-MM-dd HH:mm:ss", Inv)}', 'YYYY-MM-DD HH24:MI:SS')";
    public static string NowIso(DateTimeOffset m) => m.ToString("yyyy-MM-dd'T'HH:mm:sszzz", Inv);
    public static string NowEpoch(DateTimeOffset m) => m.ToUnixTimeSeconds().ToString(Inv);

    // ---- Convert (each non-empty line separately) ----

    /// <summary>1760356800 → TIMESTAMP '2025-10-13 12:00:00'. Seconds, ms, µs and ns are told apart by magnitude.</summary>
    public static string EpochToTimestamp(string text) => PerLine(text, s =>
    {
        if (!long.TryParse(s, NumberStyles.AllowLeadingSign, Inv, out var n) || n == long.MinValue)
            throw new FormatException($"not a Unix epoch: {s}");
        var (unit, digits) = EpochUnit(Math.Abs(n));
        var seconds = Math.DivRem(n, unit, out var fraction);
        if (fraction < 0)
        {
            seconds--;
            fraction += unit;
        }
        DateTimeOffset t;
        try
        {
            t = DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException e)
        {
            throw new FormatException($"epoch out of range: {s}", e);
        }
        var frac = digits == 0 ? "" : "." + fraction.ToString(Inv).PadLeft(digits, '0');
        return $"TIMESTAMP '{t.ToString("yyyy-MM-dd HH:mm:ss", Inv)}{frac}'";
    });

    /// <summary>Seconds, milliseconds, microseconds or nanoseconds, by magnitude (dates between 1973 and 5138).</summary>
    private static (long Unit, int Digits) EpochUnit(long abs) => abs switch
    {
        >= 100_000_000_000_000_000 => (1_000_000_000L, 9),
        >= 100_000_000_000_000 => (1_000_000L, 6),
        >= 100_000_000_000 => (1_000L, 3),
        _ => (1L, 0),
    };

    public static string ToEpoch(string text) => PerLine(text, s =>
    {
        var d = Parse(s);
        return new DateTimeOffset(d.Value, d.Offset ?? TimeSpan.Zero).ToUnixTimeSeconds().ToString(Inv);
    });

    /// <summary>Picks TO_DATE / TO_TIMESTAMP / TO_TIMESTAMP_TZ depending on what the value carries.</summary>
    public static string ToDateCall(string text) => PerLine(text, s =>
    {
        var d = Parse(s);
        if (!d.HasTime) return $"TO_DATE('{d.Value.ToString("yyyy-MM-dd", Inv)}', 'YYYY-MM-DD')";
        var value = d.Value.ToString("yyyy-MM-dd HH:mm:ss", Inv);
        var mask = "YYYY-MM-DD HH24:MI:SS";
        if (d.Fraction.Length > 0)
        {
            value += "." + d.Fraction;
            mask += ".FF";
        }
        if (d.Offset is { } off) return $"TO_TIMESTAMP_TZ('{value} {FormatOffset(off)}', '{mask} TZH:TZM')";
        return d.Fraction.Length > 0 ? $"TO_TIMESTAMP('{value}', '{mask}')" : $"TO_DATE('{value}', '{mask}')";
    });

    public static string ToLiteral(string text) => PerLine(text, s =>
    {
        var d = Parse(s);
        if (!d.HasTime) return $"DATE '{d.Value.ToString("yyyy-MM-dd", Inv)}'";
        var value = d.Value.ToString("yyyy-MM-dd HH:mm:ss", Inv) + (d.Fraction.Length > 0 ? "." + d.Fraction : "");
        if (d.Offset is { } off) value += " " + FormatOffset(off);
        return $"TIMESTAMP '{value}'";
    });

    // ---- Parsing ----

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;

    [GeneratedRegex(@"^(?:(?:DATE|TIMESTAMP)\s*)?'(?<v>[^']*)'$", Options, Limits.RegexTimeoutMs)]
    private static partial Regex QuotedLiteral();

    [GeneratedRegex(@"^TO_(?:DATE|TIMESTAMP(?:_TZ)?)\s*\(\s*'(?<v>[^']*)'", Options, Limits.RegexTimeoutMs)]
    private static partial Regex ToDateFunction();

    private const string TimePart = @"(?:(?:T|\s+)(?<h>\d{1,2}):(?<mi>\d{2})(?::(?<s>\d{2})(?:[.,](?<f>\d{1,9}))?)?)?";
    private const string OffsetPart = @"\s*(?<off>Z|[+-]\d{2}:?\d{2})?";

    [GeneratedRegex(@"^(?<y>\d{4})-(?<mo>\d{1,2})-(?<d>\d{1,2})" + TimePart + OffsetPart + "$", Options, Limits.RegexTimeoutMs)]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"^(?<d>\d{1,2})\.(?<mo>\d{1,2})\.(?<y>\d{4})" + TimePart + OffsetPart + "$", Options, Limits.RegexTimeoutMs)]
    private static partial Regex DottedDate();

    [GeneratedRegex(@"^(?<a>\d{1,2})/(?<b>\d{1,2})/(?<y>\d{4})" + TimePart + OffsetPart + "$", Options, Limits.RegexTimeoutMs)]
    private static partial Regex SlashedDate();

    /// <summary>
    /// Accepts ISO (2026-10-13[T ]14:30[:00[.123]][Z|+03:00]), dotted day.month.year, and slashed dates
    /// where the day/month order is evident (one of the two is above 12). Also unwraps DATE '...', TIMESTAMP '...',
    /// TO_DATE('...', ...) and plain '...'.
    /// </summary>
    public static ParsedDate Parse(string input)
    {
        var s = Unwrap(input.Trim());
        var (m, day, month) = MatchDate(s);
        var hasTime = m.Groups["h"].Success;
        DateTime value;
        try
        {
            value = new DateTime(Int(m, "y"), month, day,
                hasTime ? Int(m, "h") : 0, hasTime ? Int(m, "mi") : 0, m.Groups["s"].Success ? Int(m, "s") : 0,
                DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException e)
        {
            throw new FormatException($"invalid date: {s}", e);
        }
        return new ParsedDate(value, ParseOffset(m.Groups["off"]), hasTime, m.Groups["f"].Value);
    }

    private static string Unwrap(string s)
    {
        var q = QuotedLiteral().Match(s);
        if (q.Success) return q.Groups["v"].Value.Trim();
        var f = ToDateFunction().Match(s);
        return f.Success ? f.Groups["v"].Value.Trim() : s;
    }

    private static (Match Match, int Day, int Month) MatchDate(string s)
    {
        var iso = IsoDate().Match(s);
        if (iso.Success) return (iso, Int(iso, "d"), Int(iso, "mo"));

        var dotted = DottedDate().Match(s);
        if (dotted.Success) return (dotted, Int(dotted, "d"), Int(dotted, "mo"));

        var slashed = SlashedDate().Match(s);
        if (!slashed.Success) throw new FormatException($"not a recognized date: {s}");

        int a = Int(slashed, "a"), b = Int(slashed, "b");
        if (a > 12) return (slashed, a, b);
        if (b > 12 || a == b) return (slashed, b, a);
        throw new FormatException($"ambiguous date {s}: day/month order unknown, use ISO yyyy-mm-dd");
    }

    private static TimeSpan? ParseOffset(Group g)
    {
        if (!g.Success) return null;
        if (g.Value is "Z" or "z") return TimeSpan.Zero;
        var digits = g.Value[1..].Replace(":", "", StringComparison.Ordinal);
        int hours = int.Parse(digits[..2], Inv), minutes = int.Parse(digits[2..], Inv);
        // Oracle and DateTimeOffset both allow offsets within ±14:00.
        if (minutes > 59 || hours * 60 + minutes > 14 * 60) throw new FormatException($"invalid time zone offset: {g.Value}");
        var span = new TimeSpan(hours, minutes, 0);
        return g.Value[0] == '-' ? -span : span;
    }

    private static string FormatOffset(TimeSpan off) =>
        (off < TimeSpan.Zero ? "-" : "+") + off.Duration().ToString(@"hh\:mm", Inv);

    private static int Int(Match m, string group) => int.Parse(m.Groups[group].Value, Inv);

    /// <summary>
    /// Applies f to each non-empty line, keeping indentation and a trailing comma.
    /// Numeric overflow and out-of-range values in a line become FormatException (a status bar message).
    /// </summary>
    internal static string PerLine(string text, Func<string, string> f) => Lines.Map(text, lines => lines.Select(line =>
    {
        var core = line.Trim();
        if (core.Length == 0) return line;
        var indent = line[..(line.Length - line.TrimStart().Length)];
        var comma = core.EndsWith(',') ? "," : "";
        var value = core.TrimEnd(',').TrimEnd();
        try
        {
            return indent + f(value) + comma;
        }
        catch (Exception e) when (e is OverflowException or ArgumentOutOfRangeException)
        {
            throw new FormatException($"value out of range: {(value.Length > 30 ? value[..30] + "..." : value)}", e);
        }
    }).ToList());
}
