namespace TextTools.Core;

/// <summary>Operations over whole lines of the selection.</summary>
public static class LineOps
{
    public static string SortAsc(string text) => Lines.Map(text, l => l.Order(StringComparer.Ordinal));
    public static string SortDesc(string text) => Lines.Map(text, l => l.OrderDescending(StringComparer.Ordinal));
    public static string SortIgnoreCase(string text) => Lines.Map(text, l => l.Order(StringComparer.OrdinalIgnoreCase));
    public static string Reverse(string text) => Lines.Map(text, l => Enumerable.Reverse(l));
    public static string RemoveDuplicates(string text) => Lines.Map(text, l => l.Distinct(StringComparer.Ordinal));
    public static string RemoveEmpty(string text) => Lines.Map(text, l => l.Where(s => s.Trim().Length > 0));
    public static string TrimTrailing(string text) => Lines.Map(text, l => l.Select(s => s.TrimEnd()));

    /// <summary>
    /// Aligns the first occurrence of <paramref name="token"/> in each line:
    ///   l_id := 1;            l_id   := 1;
    ///   l_name := 'x';   →    l_name := 'x';
    /// Lines without the token are left as is.
    /// </summary>
    public static string AlignBy(string text, string token) => Lines.Map(text, lines =>
    {
        var parts = lines.Select((string l) =>
        {
            var i = l.IndexOf(token, StringComparison.Ordinal);
            return i < 0 ? ((string Left, string Right)?)null : (l[..i].TrimEnd(), l[(i + token.Length)..].TrimStart());
        }).ToList();
        var width = parts.Where(p => p != null).Select(p => p!.Value.Left.Length).DefaultIfEmpty(0).Max();
        return lines.Select((l, i) => parts[i] is { } p ? $"{p.Left.PadRight(width)} {token} {p.Right}".TrimEnd() : l);
    });
}
