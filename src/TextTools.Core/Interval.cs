using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TextTools.Core;

/// <summary>ISO 8601 durations (PT1H30M) → Oracle INTERVAL literals.</summary>
public static partial class Interval
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [GeneratedRegex(@"^(?<neg>-)?P(?:(?<y>\d+)Y)?(?:(?<mo>\d+)M)?(?:(?<w>\d+)W)?(?:(?<d>\d+)D)?" +
                    @"(?:T(?:(?<h>\d+)H)?(?:(?<mi>\d+)M)?(?:(?<s>\d+(?:[.,]\d+)?)S)?)?$",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, Limits.RegexTimeoutMs)]
    private static partial Regex IsoDuration();

    private static readonly string[] Components = ["y", "mo", "w", "d", "h", "mi", "s"];
    private static readonly string[] DayUnits = ["DAY", "HOUR", "MINUTE", "SECOND"];
    private static readonly decimal[] UnitSeconds = [86400, 3600, 60, 1];

    /// <summary>
    /// PT1H30M → INTERVAL '1:30' HOUR TO MINUTE; P1Y2M → INTERVAL '1-2' YEAR TO MONTH.
    /// Overflowing fields are carried (PT1H90M → '2:30'); weeks count as 7 days.
    /// Oracle cannot mix YEAR TO MONTH with DAY TO SECOND, so such durations are rejected.
    /// </summary>
    public static string FromIsoDuration(string text) => DateText.PerLine(text, s =>
    {
        var iso = s.Trim('\'', '"');
        var m = IsoDuration().Match(iso);
        bool Has(string g) => m.Groups[g].Success;
        if (!m.Success || iso.EndsWith('T') || iso.EndsWith('t') || !Components.Any(Has))
            throw new FormatException($"not an ISO 8601 duration: {s}");
        var sign = Has("neg") ? "-" : "";

        decimal Num(string g) => Has(g) ? decimal.Parse(m.Groups[g].Value.Replace(',', '.'), Inv) : 0;

        var yearMonth = Has("y") || Has("mo");
        var dayTime = Has("w") || Has("d") || Has("h") || Has("mi") || Has("s");
        if (yearMonth && dayTime)
            throw new FormatException($"{s}: Oracle cannot mix years/months with days/time in one INTERVAL");

        if (yearMonth)
        {
            var months = (long)(Num("y") * 12 + Num("mo"));
            long years = months / 12, rest = months % 12;
            if (years == 0) return $"INTERVAL '{sign}{rest}' MONTH{Precision(rest)}";
            if (rest == 0) return $"INTERVAL '{sign}{years}' YEAR{Precision(years)}";
            return $"INTERVAL '{sign}{years}-{rest}' YEAR{Precision(years)} TO MONTH";
        }

        decimal[] given = [Num("w") * 7 + Num("d"), Num("h"), Num("mi"), Num("s")];
        var total = given[0] * 86400 + given[1] * 3600 + given[2] * 60 + given[3];
        if (total == 0) return "INTERVAL '0' SECOND";

        var lead = Array.FindIndex(given, v => v != 0);
        var trail = Array.FindLastIndex(given, v => v != 0);

        var parts = new List<string>();
        var rem = total;
        decimal leadValue = 0;
        for (var i = lead; i <= trail; i++)
        {
            decimal v;
            if (i == trail)
            {
                v = rem / UnitSeconds[i];
            }
            else
            {
                v = Math.Floor(rem / UnitSeconds[i]);
                rem -= v * UnitSeconds[i];
            }
            if (i == lead) leadValue = v;
            var str = v.ToString("0.#########", Inv);
            if (i != lead)
            {
                // Non-leading fields are two digits: 1:05, 1:30.5
                var dot = str.IndexOf('.');
                str = dot < 0 ? str.PadLeft(2, '0') : str[..dot].PadLeft(2, '0') + str[dot..];
            }
            parts.Add(str);
        }

        // DAY is separated by a space, the time fields by colons: '2 10:30:05'
        var value = new StringBuilder(parts[0]);
        for (var i = 1; i < parts.Count; i++) value.Append(lead + i - 1 == 0 ? ' ' : ':').Append(parts[i]);
        var prec = Precision((long)Math.Floor(leadValue));
        var unit = lead == trail ? DayUnits[lead] + prec : $"{DayUnits[lead]}{prec} TO {DayUnits[trail]}";
        return $"INTERVAL '{sign}{value}' {unit}";
    });

    /// <summary>Oracle's default leading precision is 2 digits; larger values need e.g. MINUTE(3).</summary>
    private static string Precision(long leading)
    {
        var digits = Math.Abs(leading).ToString(Inv).Length;
        return digits > 2 ? $"({digits})" : "";
    }
}
