namespace TextTools.Core;

/// <summary>A menu entry: <see cref="Menu"/> is the submenu under Plug-Ins, <see cref="Group"/> is a block between separators.</summary>
public abstract record Command(string Menu, string Group, string Name);

/// <summary>
/// Replaces the selection (or the whole text when nothing is selected) with Transform(selection).
/// <see cref="Sample"/> is a short input whose result is shown in the status bar as a usage hint.
/// </summary>
public sealed record TransformCommand(string Menu, string Group, string Name, Func<string, string> Transform, string? Sample = null)
    : Command(Menu, Group, Name)
{
    /// <summary>"sample -> Transform(sample)", computed from the real transform so it never drifts.</summary>
    public string? Example => Sample is null ? null : $"{Sample} -> {Transform(Sample)}";
}

/// <summary>Inserts Format(now) at the cursor, replacing the selection if there is one.</summary>
public sealed record InsertCommand(string Menu, string Group, string Name, Func<DateTimeOffset, string> Format)
    : Command(Menu, Group, Name);

/// <summary>Everything that shows up in the plugin menu, in menu order. Add a line here to add a menu item.</summary>
public static class Commands
{
    public const string StringMenu = "String Tools";
    public const string DateMenu = "Date Tools";

    public static readonly IReadOnlyList<Command> All =
    [
        T(StringMenu, "Case", "camelCase", CaseConverter.ToCamel, "first_name"),
        T(StringMenu, "Case", "PascalCase", CaseConverter.ToPascal, "first_name"),
        T(StringMenu, "Case", "snake_case", CaseConverter.ToSnake, "firstName"),
        T(StringMenu, "Case", "UPPER_SNAKE_CASE", CaseConverter.ToUpperSnake, "firstName"),
        T(StringMenu, "Case", "kebab-case", CaseConverter.ToKebab, "firstName"),

        T(StringMenu, "SQL", "Escape quotes ( ' -> '' )", SqlText.Escape, "it's"),
        T(StringMenu, "SQL", "Unescape quotes ( '' -> ' )", SqlText.Unescape, "'it''s'"),
        T(StringMenu, "SQL", "Wrap in q'...' (alt quoting)", SqlText.QQuote, "it's"),
        T(StringMenu, "SQL", "Lines -> IN (...)", SqlText.ToInList),
        T(StringMenu, "SQL", "Lines -> 'a' || chr(10) || 'b'", SqlText.ToConcatenation),

        T(StringMenu, "Encode", "Base64 encode", Encode.ToBase64, "Hello"),
        T(StringMenu, "Encode", "Base64 decode", Encode.FromBase64, "SGVsbG8="),
        T(StringMenu, "Encode", "URL encode", Encode.ToUrl, "a b&c"),
        T(StringMenu, "Encode", "URL decode", Encode.FromUrl, "a%20b%26c"),
        T(StringMenu, "Encode", "HEX encode (UTF-8)", Encode.ToHex, "Hi"),
        T(StringMenu, "Encode", "HEX decode (UTF-8)", Encode.FromHex, "4869"),

        T(StringMenu, "Lines", "Sort A-Z", LineOps.SortAsc),
        T(StringMenu, "Lines", "Sort Z-A", LineOps.SortDesc),
        T(StringMenu, "Lines", "Sort A-Z, ignore case", LineOps.SortIgnoreCase),
        T(StringMenu, "Lines", "Reverse", LineOps.Reverse),
        T(StringMenu, "Lines", "Remove duplicates", LineOps.RemoveDuplicates),
        T(StringMenu, "Lines", "Remove empty lines", LineOps.RemoveEmpty),
        T(StringMenu, "Lines", "Trim trailing spaces", LineOps.TrimTrailing),
        T(StringMenu, "Lines", "Align by :=", t => LineOps.AlignBy(t, ":=")),
        T(StringMenu, "Lines", "Align by =>", t => LineOps.AlignBy(t, "=>")),

        I(DateMenu, "Now", "Now: DATE '2026-10-13'", DateText.NowDate),
        I(DateMenu, "Now", "Now: TIMESTAMP '2026-10-13 14:30:00'", DateText.NowTimestamp),
        I(DateMenu, "Now", "Now: TIMESTAMP '2026-10-13 14:30:00.123456'", DateText.NowTimestampFraction),
        I(DateMenu, "Now", "Now: TIMESTAMP WITH TIME ZONE", DateText.NowTimestampTz),
        I(DateMenu, "Now", "Now: TO_DATE('2026-10-13 14:30:00', ...)", DateText.NowToDate),
        I(DateMenu, "Now", "Now: ISO 8601 (2026-10-13T14:30:00-03:00)", DateText.NowIso),
        I(DateMenu, "Now", "Now: Unix epoch (seconds)", DateText.NowEpoch),

        T(DateMenu, "Convert", "Epoch -> TIMESTAMP (UTC)", DateText.EpochToTimestamp, "1760356800"),
        T(DateMenu, "Convert", "Date -> Unix epoch (UTC if no offset)", DateText.ToEpoch, "2025-10-13 12:00:00"),
        T(DateMenu, "Convert", "Date -> TO_DATE(...)", DateText.ToDateCall, "13.10.2026 14:30"),
        T(DateMenu, "Convert", "Date -> DATE / TIMESTAMP literal", DateText.ToLiteral, "13.10.2026"),

        T(DateMenu, "Masks", "Java/.NET mask -> Oracle mask", DateMasks.ToOracle, "yyyy-MM-dd HH:mm:ss"),
        T(DateMenu, "Masks", "Oracle mask -> Java mask", DateMasks.ToJava, "YYYY-MM-DD HH24:MI:SS"),
        T(DateMenu, "Masks", "Oracle mask -> .NET mask", DateMasks.ToDotNet, "DD.MM.YYYY HH24:MI:SS.FF3"),
        T(DateMenu, "Masks", "ISO duration -> INTERVAL", Interval.FromIsoDuration, "PT1H30M"),
    ];

    private static TransformCommand T(string menu, string group, string name, Func<string, string> f, string? sample = null) =>
        new(menu, group, name, f, sample);
    private static InsertCommand I(string menu, string group, string name, Func<DateTimeOffset, string> f) => new(menu, group, name, f);
}
