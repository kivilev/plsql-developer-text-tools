using System.Text.Json;

namespace TextTools.Core.Formatting;

/// <summary>
/// The values of all settings (<see cref="FormatSettings.All"/>): what the settings window edits and the profile file holds.
/// Unknown ids in a file are ignored, a missing or invalid value falls back to the default, so an old file never breaks a new plug-in.
/// </summary>
public sealed class FormatProfile
{
    private readonly Dictionary<string, int> _values = new(StringComparer.Ordinal);

    public FormatProfile()
    {
        foreach (var s in FormatSettings.All) _values[s.Id] = s.Default;
    }

    public int Get(string id) => _values.TryGetValue(id, out var v) ? v : 0;

    public bool IsOn(string id) => Get(id) != 0;

    /// <summary>Sets a known setting; the value is brought into its range. Unknown ids are ignored.</summary>
    public void Set(string id, int value)
    {
        if (FormatSettings.Find(id) is not { } setting) return;
        var allowed = Math.Clamp(value, setting.Min, setting.Max);
        if (setting.Kind == SettingKind.Bool) allowed = value != 0 ? 1 : 0;
        _values[id] = allowed;
    }

    public FormatProfile Clone()
    {
        var copy = new FormatProfile();
        foreach (var (id, value) in _values) copy._values[id] = value;
        return copy;
    }

    /// <summary>The options the engine runs with.</summary>
    public FormatOptions ToOptions() => new(
        Margin: Get("margin"),
        BuiltinFunctionsUpper: IsOn("builtinFunctionsUpper"),
        AttributesUpper: IsOn("attributesUpper"),
        EndWithName: IsOn("endWithName"),
        AlignAssignments: IsOn("alignAssignments"),
        BlankLineBeforeSubprograms: IsOn("blankLineBeforeSubprograms"),
        BlankLineAfterPackageHeader: IsOn("blankLineAfterPackageHeader"),
        NoBlankLineBeforePackageEnd: IsOn("noBlankLineBeforePackageEnd"),
        ExecuteImmediateLiteralOnOwnLine: IsOn("executeImmediateLiteralOnOwnLine"),
        TrimTrailingSpaces: IsOn("trimTrailingSpaces"),
        SpaceAfterComma: IsOn("spaceAfterComma"),
        TightParentheses: IsOn("tightParentheses"),
        SelectIntoOnOneLine: IsOn("selectIntoOnOneLine"),
        UpdateSetLayout: IsOn("updateSetLayout"),
        JoinIfConditions: IsOn("joinIfConditions"),
        JoinOnAlignment: IsOn("joinOnAlignment"),
        ShortParenGroupOnOneLine: IsOn("shortParenGroupOnOneLine"),
        InsertLayout: IsOn("insertLayout"),
        HeaderTailsOnOneLine: IsOn("headerTailsOnOneLine"),
        SelectColumnsOnePerLine: IsOn("selectColumnsOnePerLine"),
        SubqueryLayout: IsOn("subqueryLayout"),
        SingleBlankBeforeAlias: !IsOn("alignAliases"),
        WrapLongLines: IsOn("wrapLongLines"),
        WrapDecodeInPairs: IsOn("wrapDecodeInPairs"),
        WrapBetween: IsOn("wrapBetween"),
        AlignNamedArguments: IsOn("alignNamedArguments"),
        AlignDeclarations: IsOn("alignDeclarations"),
        IndentComments: IsOn("indentComments"));

    /// <summary>{ "version": 1, "settings": { "margin": 76, "endWithName": true, ... } }, indented so that it diffs well in a repository.</summary>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("version", 1);
            w.WriteStartObject("settings");
            foreach (var s in FormatSettings.All)
            {
                if (s.Kind == SettingKind.Bool) w.WriteBoolean(s.Id, IsOn(s.Id));
                else w.WriteNumber(s.Id, Get(s.Id));
            }
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The profile of a file; the default profile when the text is not a profile at all.</summary>
    public static FormatProfile FromJson(string? json)
    {
        var profile = new FormatProfile();
        if (string.IsNullOrWhiteSpace(json)) return profile;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("settings", out var settings)
                || settings.ValueKind != JsonValueKind.Object)
            {
                return profile;
            }
            foreach (var p in settings.EnumerateObject())
            {
                switch (p.Value.ValueKind)
                {
                    case JsonValueKind.True:
                        profile.Set(p.Name, 1);
                        break;
                    case JsonValueKind.False:
                        profile.Set(p.Name, 0);
                        break;
                    case JsonValueKind.Number when p.Value.TryGetInt32(out var n):
                        profile.Set(p.Name, n);
                        break;
                }
            }
        }
        catch (JsonException)
        {
            return new FormatProfile();
        }
        return profile;
    }

    /// <summary>
    /// The profile as a text file in the style of the Beautifier's rules file of PL/SQL Developer (<c>.br</c>): <c>Key=Value</c> per line,
    /// <c>Version=1</c> first, <c>TRUE</c> / <c>FALSE</c> for switches, the setting id with a capital first letter as the key.
    /// </summary>
    public string ToRulesText()
    {
        var sb = new System.Text.StringBuilder("Version=1\r\n");
        foreach (var s in FormatSettings.All)
        {
            var key = char.ToUpperInvariant(s.Id[0]) + s.Id[1..];
            var value = Get(s.Id).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (s.Kind == SettingKind.Bool) value = IsOn(s.Id) ? "TRUE" : "FALSE";
            sb.Append(key).Append('=').Append(value).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Reads a rules text into this profile: our own file, or the rules file of PL/SQL Developer (its keys are known from
    /// <see cref="FormatSetting.Sources"/>, e.g. <c>RightMargin</c>, <c>AlignAssignmentGroups</c>). Unknown keys are ignored, settings
    /// that the text does not mention keep their value. Returns the number of settings that were set.
    /// </summary>
    public int ReadRulesText(string? text)
    {
        var applied = 0;
        if (string.IsNullOrWhiteSpace(text)) return 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            var setting = FormatSettings.All.FirstOrDefault(s => string.Equals(s.Id, key, StringComparison.OrdinalIgnoreCase))
                ?? FormatSettings.All.FirstOrDefault(s => s.Sources is { } src && src.TryGetValue("PLSQLDev", out var name) && string.Equals(name, key, StringComparison.OrdinalIgnoreCase));
            if (setting is null) continue;
            int number;
            if (string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase)) number = 1;
            else if (string.Equals(value, "FALSE", StringComparison.OrdinalIgnoreCase)) number = 0;
            else if (!int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out number)) continue;
            Set(setting.Id, number);
            applied++;
        }
        return applied;
    }

    /// <summary>A new profile (the defaults) with a rules text read into it.</summary>
    public static FormatProfile FromRulesText(string? text, out int applied)
    {
        var profile = new FormatProfile();
        applied = profile.ReadRulesText(text);
        return profile;
    }
}
