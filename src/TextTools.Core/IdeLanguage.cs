namespace TextTools.Core;

/// <summary>
/// The languages of the IDE's translations (Preferences: Language; the Languages folder of PL/SQL Developer) that the
/// plug-in speaks. English is the source text and the fallback; every other language has its own
/// <c>captions.&lt;code&gt;.json</c> and nothing else may show English text to a user of that language.
/// </summary>
public static class IdeLanguage
{
    /// <summary>Language code and the markers of the names the IDE gives it: <c>IDE_TranslationLanguage</c> ("Russian") or the file name ("Russian.lang").</summary>
    private static readonly (string Code, string[] Markers)[] Known =
    [
        ("pt", ["Brazil", "Portug", "Português", "Portugues"]),
        ("zh", ["Chinese", "中文", "简体", "繁體", "Chinês"]),
        ("fr", ["French", "Français", "Francais"]),
        ("de", ["German", "Deutsch"]),
        ("hu", ["Hungar", "Magyar"]),
        ("ja", ["Japan", "日本"]),
        ("ru", ["Russian", "Русск"]),
        ("es", ["Spanish", "Español", "Espanol"]),
    ];

    /// <summary>The codes of the supported languages other than English.</summary>
    public static IReadOnlyList<string> Codes { get; } = [.. Known.Select(k => k.Code)];

    /// <summary>"Russian", "Russian.lang", "Русский" or the code itself -> "ru"; null for English, empty and anything unknown.</summary>
    public static string? CodeOf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var (code, markers) in Known)
        {
            if (string.Equals(name.Trim(), code, StringComparison.OrdinalIgnoreCase)) return code;
            if (markers.Any(m => name.Contains(m, StringComparison.OrdinalIgnoreCase))) return code;
        }
        return null;
    }

    /// <summary>The language by its name, else by the translation file name.</summary>
    public static string? Resolve(string? language, string? file) => CodeOf(language) ?? CodeOf(file);
}
