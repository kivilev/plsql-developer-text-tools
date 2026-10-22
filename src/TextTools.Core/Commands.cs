using TextTools.Core.Sql;

namespace TextTools.Core;

/// <summary>A menu entry: <see cref="Menu"/> is the submenu under Plug-Ins, <see cref="Group"/> is a block between separators.</summary>
public abstract record Command(string Menu, string Group, string Name);

/// <summary>
/// Replaces the selection (or the whole text when nothing is selected) with Transform(selection).
/// <see cref="Sample"/> is an input whose result documents the command (docs/commands.md) and, when it is a single
/// short line, is shown in the status bar as a usage hint.
/// </summary>
public sealed record TransformCommand(string Menu, string Group, string Name, Func<string, string> Transform, string? Sample = null)
    : Command(Menu, Group, Name)
{
    /// <summary>"sample -> Transform(sample)", computed from the real transform so it never drifts.</summary>
    public string? Example => Sample is null ? null : $"{Sample} -> {Transform(Sample)}";

    /// <summary>The example for the status bar: only a single short line fits there.</summary>
    public string? StatusExample =>
        Example is { Length: <= MaxStatusExample } e && !e.Contains('\n', StringComparison.Ordinal) ? e : null;

    public const int MaxStatusExample = 80;
}

/// <summary>Inserts Format(now) at the cursor, replacing the selection if there is one.</summary>
public sealed record InsertCommand(string Menu, string Group, string Name, Func<DateTimeOffset, string> Format)
    : Command(Menu, Group, Name);

/// <summary>
/// Builds text for the clipboard from the editor text and the selection or cursor; the editor is not changed.
/// The second argument is the database (package specifications), null when not connected.
/// <see cref="Sample"/> is an editor text with the cursor at <see cref="SampleCursor"/> (docs/commands.md).
/// </summary>
public sealed record CopyCommand(string Menu, string Group, string Name, Func<SqlRequest, ISpecSource?, CopyResult> Build, string Sample, int SampleCursor)
    : Command(Menu, Group, Name)
{
    public string SampleResult => Build(new SqlRequest(Sample, 0, 0, SampleCursor), null).Text;
}

/// <summary>Where an explain plan command shows the plan.</summary>
public enum PlanView
{
    /// <summary>The IDE's own Explain Plan window, executed at once.</summary>
    IdeWindow,

    /// <summary>The plug-in's HTML window with the Plan and Findings tabs.</summary>
    Extended,
}

/// <summary>
/// Explain plan of the statement under the cursor (or the selection) with constants resolved exactly like
/// <see cref="CopyCommand"/>; the editor is not changed.
/// </summary>
public sealed record PlanCommand(string Menu, string Group, string Name, PlanView View) : Command(Menu, Group, Name);

/// <summary>Everything that shows up in the plugin menu, in menu order. Add a line here to add a menu item.</summary>
public static class Commands
{
    public const string StringMenu = "String Tools";
    public const string DateMenu = "Date Tools";
    public const string SqlMenu = "SQL Tools";

    /// <summary>
    /// Commands also shown in the editor's context menu, in this order. The IDE draws plug-in context items as a flat
    /// list (no submenus), so only the everyday ones go there; everything else is in the Plug-Ins menu.
    /// </summary>
    public static readonly IReadOnlyList<string> ContextMenu =
    [
        "Copy SQL with constants resolved",
        "Explain Plan with constants",
        "List -> IN (...)",
        "Escape quotes ( ' -> '' )",
        "Unescape quotes ( '' -> ' )",
        "Wrap in q'...' (alt quoting)",
        "snake_case",
        "camelCase",
        "Now: DATE '2026-10-13'",
        "Date -> TO_DATE(...)",
    ];

    private const string CopySample =
        "declare\n  c_new constant varchar2(3) := 'NEW';\n  l_cnt number;\nbegin\n" +
        "  select count(*) into l_cnt from orders where status = c_new;\nend;";

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
        T(StringMenu, "SQL", "List -> IN (...)", SqlText.ToInList, "42, O'Neil"),
        T(StringMenu, "SQL", "Lines -> 'a' || chr(10) || 'b'", SqlText.ToConcatenation, "Dear\nit's ok"),

        T(StringMenu, "Encode", "Base64 encode", Encode.ToBase64, "Hello"),
        T(StringMenu, "Encode", "Base64 decode", Encode.FromBase64, "SGVsbG8="),
        T(StringMenu, "Encode", "URL encode", Encode.ToUrl, "a b&c"),
        T(StringMenu, "Encode", "URL decode", Encode.FromUrl, "a%20b%26c"),
        T(StringMenu, "Encode", "HEX encode (UTF-8)", Encode.ToHex, "Hi"),
        T(StringMenu, "Encode", "HEX decode (UTF-8)", Encode.FromHex, "4869"),

        T(StringMenu, "Lines", "Sort A-Z", LineOps.SortAsc, "b\nC\na"),
        T(StringMenu, "Lines", "Sort Z-A", LineOps.SortDesc, "b\nC\na"),
        T(StringMenu, "Lines", "Sort A-Z, ignore case", LineOps.SortIgnoreCase, "b\nC\na"),
        T(StringMenu, "Lines", "Reverse", LineOps.Reverse, "1\n2\n3"),
        T(StringMenu, "Lines", "Remove duplicates", LineOps.RemoveDuplicates, "a\nb\na"),
        T(StringMenu, "Lines", "Remove empty lines", LineOps.RemoveEmpty, "a\n\nb"),
        T(StringMenu, "Lines", "Trim trailing spaces", LineOps.TrimTrailing, "a   \nb  "),
        T(StringMenu, "Lines", "Align by :=", t => LineOps.AlignBy(t, ":="), "l_id := 1;\nl_name := 'x';"),
        T(StringMenu, "Lines", "Align by =>", t => LineOps.AlignBy(t, "=>"), "p_id => 1,\np_name => 'x'"),

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

        new CopyCommand(SqlMenu, "Copy", "Copy SQL with constants resolved", ConstantResolver.CopySql,
            CopySample, CopySample.IndexOf("select", StringComparison.Ordinal)),

        new PlanCommand(SqlMenu, "Copy", "Explain Plan with constants", PlanView.IdeWindow),
        // Extended Explain Plan (PlanView.Extended) is not in the menu until it is stable: see the draft PR.
    ];

    private static TransformCommand T(string menu, string group, string name, Func<string, string> f, string? sample = null) =>
        new(menu, group, name, f, sample);
    private static InsertCommand I(string menu, string group, string name, Func<DateTimeOffset, string> f) => new(menu, group, name, f);
}
