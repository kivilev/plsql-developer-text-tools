using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace TextTools.Core;

/// <summary>
/// The captions of the plug-in menu in the language of the IDE's translation. The English caption (the name in
/// <see cref="Commands"/>) is the identity of a command; a translation only changes what the user reads. Captions are data:
/// <c>Menu/captions.&lt;code&gt;.json</c> in the DLL, English caption -> translated caption. A caption without a
/// translation stays English.
/// </summary>
public sealed class MenuCaptions
{
    private static readonly ConcurrentDictionary<string, MenuCaptions> ByLanguage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _captions;

    private MenuCaptions(string language, Dictionary<string, string> captions)
    {
        Language = language;
        _captions = captions;
    }

    /// <summary>The English captions: no translation.</summary>
    public static MenuCaptions English { get; } = new("", []);

    /// <summary>The language code ("ru"), empty for English.</summary>
    public string Language { get; }

    /// <summary>The captions for the IDE's translation language / file name; English when unknown.</summary>
    public static MenuCaptions For(string? language, string? file = null) =>
        IdeLanguage.Resolve(language, file) is { } code ? ByLanguage.GetOrAdd(code, Load) : English;

    /// <summary>The translated caption, or <paramref name="english"/> when there is none.</summary>
    public string Caption(string english) => _captions.TryGetValue(english, out var t) && t.Length > 0 ? t : english;

    private static MenuCaptions Load(string code)
    {
        using var stream = typeof(MenuCaptions).Assembly.GetManifestResourceStream($"TextTools.Core.Menu.captions.{code}.json");
        if (stream is null) return English;
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var doc = JsonDocument.Parse(reader.ReadToEnd());
            var captions = doc.RootElement.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.Ordinal);
            return new MenuCaptions(code, captions);
        }
        catch (JsonException)
        {
            return English;
        }
    }
}
