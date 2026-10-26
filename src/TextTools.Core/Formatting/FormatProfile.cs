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
        NoBlankLineBeforePackageEnd: IsOn("noBlankLineBeforePackageEnd"),
        ExecuteImmediateLiteralOnOwnLine: IsOn("executeImmediateLiteralOnOwnLine"),
        TrimTrailingSpaces: IsOn("trimTrailingSpaces"));

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
}
