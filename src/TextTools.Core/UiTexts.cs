using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace TextTools.Core;

/// <summary>
/// The texts of the plug-in's own windows in the language of the IDE's translation: <c>Ui/texts.json</c> (English, the
/// source and the fallback) with <c>Ui/texts.&lt;code&gt;.json</c> laid over it. A key without a translation shows English;
/// a key that does not exist at all shows the key itself.
/// </summary>
public sealed class UiTexts
{
    private static readonly ConcurrentDictionary<string, UiTexts> ByLanguage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _texts;

    private UiTexts(string language, Dictionary<string, string> texts)
    {
        Language = language;
        _texts = texts;
    }

    /// <summary>The English texts.</summary>
    public static UiTexts English { get; } = new("", Read("TextTools.Core.Ui.texts.json") ?? []);

    /// <summary>The language code ("ru"), empty for English.</summary>
    public string Language { get; }

    /// <summary>The texts for the IDE's translation language / file name; English when unknown.</summary>
    public static UiTexts For(string? language, string? file = null) =>
        IdeLanguage.Resolve(language, file) is { } code ? ByLanguage.GetOrAdd(code, Load) : English;

    public string Text(string key) => _texts.TryGetValue(key, out var t) && t.Length > 0 ? t : key;

    private static UiTexts Load(string code)
    {
        var translated = Read($"TextTools.Core.Ui.texts.{code}.json");
        if (translated is null) return English;
        var merged = new Dictionary<string, string>(English._texts, StringComparer.Ordinal);
        foreach (var (key, text) in translated)
        {
            if (merged.ContainsKey(key) && text.Length > 0) merged[key] = text;
        }
        return new UiTexts(code, merged);
    }

    private static Dictionary<string, string>? Read(string resource)
    {
        using var stream = typeof(UiTexts).Assembly.GetManifestResourceStream(resource);
        if (stream is null) return null;
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var doc = JsonDocument.Parse(reader.ReadToEnd());
            return doc.RootElement.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
