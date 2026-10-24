namespace TextTools.Core.Formatting;

/// <summary>What a setting holds.</summary>
public enum SettingKind
{
    /// <summary>On or off (stored as 0 or 1).</summary>
    Bool,

    /// <summary>A whole number between <see cref="FormatSetting.Min"/> and <see cref="FormatSetting.Max"/>.</summary>
    Number,
}

/// <summary>
/// One setting of the formatting rules. Settings are the common vocabulary of the formatter: the settings window is built from
/// this list, and a setting that several IDEs have under different names is listed once, with those names in
/// <see cref="Sources"/> (the key for importing a profile of another IDE).
/// </summary>
/// <param name="Id">Stable key in the profile file.</param>
/// <param name="Tab">The tab of the settings window; its text is <c>tab.&lt;Tab&gt;</c> in <c>Ui/texts.json</c>, the label is <c>setting.&lt;Id&gt;</c>.</param>
/// <param name="Kind">Bool or Int.</param>
/// <param name="Default">The value of the default profile.</param>
/// <param name="Min">Lowest allowed value (Number).</param>
/// <param name="Max">Highest allowed value (Number).</param>
/// <param name="Sources">The same setting in other tools: key = tool ("PLSQLDev", "SQLDev", "DataGrip", "ICBS"), value = its name for it.</param>
public sealed record FormatSetting(
    string Id,
    string Tab,
    SettingKind Kind,
    int Default,
    int Min = 0,
    int Max = 1,
    IReadOnlyDictionary<string, string>? Sources = null);

/// <summary>The settings the engine has today, in the order of the settings window. A rule is added here when the engine gets it.</summary>
public static class FormatSettings
{
    public static readonly IReadOnlyList<FormatSetting> All =
    [
        new("margin", "general", SettingKind.Number, 76, 40, 200, Src(("PLSQLDev", "RightMargin"), ("SQLDev", "maxCharLineSize"), ("DataGrip", "Right margin"))),

        new("builtinFunctionsUpper", "case", SettingKind.Bool, 1, Sources: Src(("ICBS", "R04"))),
        new("attributesUpper", "case", SettingKind.Bool, 1, Sources: Src(("ICBS", "R06"))),

        new("endWithName", "code", SettingKind.Bool, 1, Sources: Src(("ICBS", "R07"))),
        new("alignAssignments", "code", SettingKind.Bool, 1, Sources: Src(("PLSQLDev", "AlignAssignmentGroups"), ("SQLDev", "alignAssignments"), ("DataGrip", "Align assignments"), ("ICBS", "R08"))),
        new("executeImmediateLiteralOnOwnLine", "code", SettingKind.Bool, 1, Sources: Src(("ICBS", "R21"))),
        new("alignDeclarations", "code", SettingKind.Bool, 1, Sources: Src(("PLSQLDev", "AlignDeclarations"), ("SQLDev", "alignDeclarations"))),
        new("indentComments", "code", SettingKind.Bool, 1),
        new("headerTailsOnOneLine", "code", SettingKind.Bool, 1, Sources: Src(("ICBS", "R18"))),
        new("tightParentheses", "code", SettingKind.Bool, 1),
        new("spaceAfterComma", "code", SettingKind.Bool, 0, Sources: Src(("DataGrip", "Spaces > Other > After comma"))),

        new("insertLayout", "sql", SettingKind.Bool, 1, Sources: Src(("ICBS", "R13"))),
        new("selectColumnsOnePerLine", "sql", SettingKind.Bool, 1, Sources: Src(("ICBS", "R15"))),
        new("selectIntoOnOneLine", "sql", SettingKind.Bool, 1, Sources: Src(("ICBS", "R15"))),
        new("updateSetLayout", "sql", SettingKind.Bool, 1, Sources: Src(("ICBS", "R10"))),
        new("joinOnAlignment", "sql", SettingKind.Bool, 1, Sources: Src(("ICBS", "R12"))),
        new("subqueryLayout", "sql", SettingKind.Bool, 1),
        new("alignAliases", "sql", SettingKind.Bool, 1, Sources: Src(("PLSQLDev", "AlignAliases"), ("SQLDev", "alignColumnAliases"), ("DataGrip", "Align column aliases"))),

        new("wrapLongLines", "wrapping", SettingKind.Bool, 1, Sources: Src(("ICBS", "R02"))),
        new("wrapDecodeInPairs", "wrapping", SettingKind.Bool, 1),
        new("wrapBetween", "wrapping", SettingKind.Bool, 1),
        new("alignNamedArguments", "wrapping", SettingKind.Bool, 1, Sources: Src(("ICBS", "R09"))),
        new("joinIfConditions", "wrapping", SettingKind.Bool, 1, Sources: Src(("ICBS", "R11"))),
        new("shortParenGroupOnOneLine", "wrapping", SettingKind.Bool, 1, Sources: Src(("ICBS", "R14"))),

        new("blankLineBeforeSubprograms", "blank", SettingKind.Bool, 1, Sources: Src(("ICBS", "R19"))),
        new("blankLineAfterPackageHeader", "blank", SettingKind.Bool, 0),
        new("noBlankLineBeforePackageEnd", "blank", SettingKind.Bool, 1, Sources: Src(("ICBS", "R20"))),
        new("trimTrailingSpaces", "blank", SettingKind.Bool, 1),
    ];

    /// <summary>The tabs of the settings window in order.</summary>
    public static IReadOnlyList<string> Tabs { get; } = [.. All.Select(s => s.Tab).Distinct(StringComparer.Ordinal)];

    public static FormatSetting? Find(string id) => All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    private static Dictionary<string, string> Src(params (string Tool, string Name)[] sources) =>
        sources.ToDictionary(s => s.Tool, s => s.Name, StringComparer.Ordinal);
}
