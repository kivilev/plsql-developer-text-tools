using System.Globalization;

namespace TextTools.Core.Plan;

/// <summary>Number formatting of the report: invariant, grouped, no NLS surprises.</summary>
public static class PlanFormat
{
    public static string Count(long value) => value.ToString("#,0", CultureInfo.InvariantCulture);

    public static string Count(long? value) => value is { } v ? Count(v) : "";

    /// <summary>Share as a percentage with one significant decimal for small values: 0.3%, 12%.</summary>
    public static string Percent(double share) =>
        share < 0.1 ? (share * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%" : (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>Width (0..100) of a logarithmic bar: equal steps for 10x, so jumps between steps stand out.</summary>
    public static int LogBar(long? value, long max)
    {
        if (value is not { } v || v <= 0 || max <= 0) return 0;
        var top = Math.Log10(max + 1.0);
        return top <= 0 ? 0 : (int)Math.Round(Math.Clamp(Math.Log10(v + 1.0) / top, 0, 1) * 100);
    }
}
