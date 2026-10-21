using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace TextTools.Core.Plan;

/// <summary>What an operation does, when it is normal, when suspicious, what to check.</summary>
public sealed record OperationInfo(string What, string Normal, string Suspicious, string Check);

/// <summary>A Note phrase of DBMS_XPLAN with its meaning and what to do.</summary>
public sealed record NoteInfo(string Match, string Meaning, string Action);

/// <summary>Texts and level of a rule; {placeholders} are filled by the rule.</summary>
public sealed record RuleText(Level Level, string Title, string Detail, string Normal);

/// <summary>
/// The reference and the rule texts: data, not code (Plan/reference.json embedded in the DLL). Parsed with
/// JsonDocument, which needs no reflection (Native AOT).
/// </summary>
public sealed class PlanReference
{
    private readonly Dictionary<string, OperationInfo> _operations;
    private readonly Dictionary<string, RuleText> _rules;
    private readonly Dictionary<string, double> _thresholds;
    private readonly Dictionary<string, string> _ui;
    private static readonly ConcurrentDictionary<string, PlanReference> ByLanguage = new(StringComparer.Ordinal);

    private PlanReference(
        string courseUrl,
        Dictionary<string, OperationInfo> operations,
        Dictionary<string, string> columns,
        List<NoteInfo> notes,
        Dictionary<string, RuleText> rules,
        Dictionary<string, double> thresholds,
        List<long> picklerRows,
        Dictionary<string, string> banners,
        Dictionary<string, string> ui)
    {
        CourseUrl = courseUrl;
        _operations = operations;
        Columns = columns;
        Notes = notes;
        _rules = rules;
        _thresholds = thresholds;
        PicklerDefaultRows = picklerRows;
        Banners = banners;
        _ui = ui;
    }

    /// <summary>The reference embedded in this assembly.</summary>
    public static PlanReference Default { get; } = Load();

    /// <summary>The report language code of this reference ("" = English).</summary>
    public string Language { get; private init; } = "";

    public string CourseUrl { get; }

    public IReadOnlyDictionary<string, string> Columns { get; }

    public IReadOnlyList<NoteInfo> Notes { get; }

    public IReadOnlyList<long> PicklerDefaultRows { get; }

    public IReadOnlyDictionary<string, string> Banners { get; }

    public IReadOnlyDictionary<string, OperationInfo> Operations => _operations;

    /// <summary>"TABLE ACCESS FULL" first, then "TABLE ACCESS".</summary>
    public OperationInfo? Operation(PlanRow row) =>
        _operations.GetValueOrDefault(row.FullOperation) ?? _operations.GetValueOrDefault(row.Operation);

    /// <summary>The label or sentence of the report; the key itself when the reference has no such text.</summary>
    public string Ui(string key) => _ui.TryGetValue(key, out var v) ? v : key;

    public string Ui(string key, params (string Name, string Value)[] values) =>
        Fill(Ui(key), values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    /// <summary>
    /// The reference in the language of the IDE's translation (the name <c>IDE_TranslationLanguage</c> or the file name
    /// <c>IDE_TranslationFile</c> returns): English texts with the translated ones laid over them. Unknown language: English.
    /// </summary>
    public static PlanReference For(string? language, string? file = null)
    {
        var code = LanguageCode(language) ?? LanguageCode(file);
        return code is null ? Default : ByLanguage.GetOrAdd(code, LoadLanguage);
    }

    /// <summary>"Russian", "Russian.lang", "Русский" -> "ru"; null for English and anything unknown.</summary>
    public static string? LanguageCode(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var (code, markers) in Languages)
        {
            if (string.Equals(name.Trim(), code, StringComparison.OrdinalIgnoreCase)) return code;
            if (markers.Any(m => name.Contains(m, StringComparison.OrdinalIgnoreCase))) return code;
        }
        return null;
    }

    private static readonly (string Code, string[] Markers)[] Languages =
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

    /// <summary>The report languages: the codes of the <c>reference.&lt;code&gt;.json</c> files, one per translation of the IDE.</summary>
    public static IReadOnlyList<string> LanguageCodes { get; } = [.. Languages.Select(l => l.Code)];

    public NoteInfo? Note(string note) => Notes.FirstOrDefault(n => note.Contains(n.Match, StringComparison.OrdinalIgnoreCase));

    public RuleText Rule(string id) =>
        _rules.TryGetValue(id, out var r) ? r : new RuleText(Level.Info, id, "", "");

    public double Threshold(string name, double fallback) => _thresholds.GetValueOrDefault(name, fallback);

    /// <summary>The course link of a finding (UTM content = rule id).</summary>
    public string CourseLink(string ruleId) => CourseUrl.Replace("{rule}", Uri.EscapeDataString(ruleId), StringComparison.Ordinal);

    /// <summary>Fills {name} placeholders; unknown ones are left as they are.</summary>
    public static string Fill(string template, IReadOnlyDictionary<string, string> values)
    {
        var sb = new StringBuilder(template.Length + 32);
        var i = 0;
        while (i < template.Length)
        {
            var open = template.IndexOf('{', i);
            var close = open < 0 ? -1 : template.IndexOf('}', open + 1);
            if (open < 0 || close < 0)
            {
                sb.Append(template, i, template.Length - i);
                break;
            }
            sb.Append(template, i, open - i);
            var key = template[(open + 1)..close];
            sb.Append(values.TryGetValue(key, out var v) ? v : template[open..(close + 1)]);
            i = close + 1;
        }
        return sb.ToString();
    }

    public static PlanReference Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var operations = new Dictionary<string, OperationInfo>(StringComparer.Ordinal);
        foreach (var p in Object(root, "operations"))
        {
            operations[p.Name] = new OperationInfo(Str(p.Value, "what"), Str(p.Value, "normal"), Str(p.Value, "suspicious"), Str(p.Value, "check"));
        }
        var columns = Object(root, "columns").ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.Ordinal);
        var notes = new List<NoteInfo>();
        if (root.TryGetProperty("notes", out var n) && n.ValueKind == JsonValueKind.Array)
        {
            notes.AddRange(n.EnumerateArray().Select(e => new NoteInfo(Str(e, "match"), Str(e, "meaning"), Str(e, "action"))));
        }
        var rules = new Dictionary<string, RuleText>(StringComparer.Ordinal);
        foreach (var p in Object(root, "rules"))
        {
            rules[p.Name] = new RuleText(LevelOf(Str(p.Value, "level")), Str(p.Value, "title"), Str(p.Value, "detail"), Str(p.Value, "normal"));
        }
        var thresholds = new Dictionary<string, double>(StringComparer.Ordinal);
        var pickler = new List<long>();
        foreach (var p in Object(root, "thresholds"))
        {
            if (p.Value.ValueKind == JsonValueKind.Number)
            {
                thresholds[p.Name] = p.Value.GetDouble();
            }
            else if (p.Value.ValueKind == JsonValueKind.Array && string.Equals(p.Name, "picklerDefaultRows", StringComparison.Ordinal))
            {
                pickler.AddRange(p.Value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Number).Select(e => e.GetInt64()));
            }
        }
        var banners = Object(root, "banners").ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.Ordinal);
        var ui = Object(root, "ui").ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.Ordinal);
        return new PlanReference(Str(root, "courseUrl"), operations, columns, notes, rules, thresholds, pickler, banners, ui);
    }

    /// <summary>
    /// This reference with the texts of a translation laid over it (the same JSON sections; notes keyed by their match
    /// text). A key the translation lacks, or leaves empty, keeps its English text.
    /// </summary>
    public PlanReference WithTranslation(string json, string language)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string Or(JsonElement e, string name, string fallback) => Str(e, name) is { Length: > 0 } t ? t : fallback;
        Dictionary<string, string> Texts(IEnumerable<KeyValuePair<string, string>> english, string section)
        {
            var result = new Dictionary<string, string>(english, StringComparer.Ordinal);
            foreach (var p in Object(root, section))
            {
                if (result.ContainsKey(p.Name) && p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 } t) result[p.Name] = t;
            }
            return result;
        }

        var operations = new Dictionary<string, OperationInfo>(_operations, StringComparer.Ordinal);
        foreach (var p in Object(root, "operations"))
        {
            if (_operations.TryGetValue(p.Name, out var o))
            {
                operations[p.Name] = new OperationInfo(Or(p.Value, "what", o.What), Or(p.Value, "normal", o.Normal), Or(p.Value, "suspicious", o.Suspicious), Or(p.Value, "check", o.Check));
            }
        }
        var translatedNotes = Object(root, "notes").ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
        var notes = Notes.Select(n => translatedNotes.TryGetValue(n.Match, out var t)
            ? new NoteInfo(n.Match, Or(t, "meaning", n.Meaning), Or(t, "action", n.Action))
            : n).ToList();
        var rules = new Dictionary<string, RuleText>(_rules, StringComparer.Ordinal);
        foreach (var p in Object(root, "rules"))
        {
            if (_rules.TryGetValue(p.Name, out var r)) rules[p.Name] = new RuleText(r.Level, Or(p.Value, "title", r.Title), Or(p.Value, "detail", r.Detail), Or(p.Value, "normal", r.Normal));
        }
        return new PlanReference(
            CourseUrl, operations, Texts(Columns, "columns"), notes, rules,
            _thresholds, [.. PicklerDefaultRows], Texts(Banners, "banners"), Texts(_ui, "ui"))
        {
            Language = language,
        };
    }

    private static PlanReference LoadLanguage(string code)
    {
        using var stream = typeof(PlanReference).Assembly.GetManifestResourceStream($"TextTools.Core.Plan.reference.{code}.json");
        if (stream is null) return Default;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        try
        {
            return Default.WithTranslation(reader.ReadToEnd(), code);
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    private static PlanReference Load()
    {
        using var stream = typeof(PlanReference).Assembly.GetManifestResourceStream("TextTools.Core.Plan.reference.json")
            ?? throw new InvalidOperationException("Plan reference is missing from the assembly.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    private static Level LevelOf(string s) => s switch
    {
        "P" => Level.Problem,
        "C" => Level.Check,
        _ => Level.Info,
    };

    private static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static List<JsonProperty> Object(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? [.. v.EnumerateObject()] : [];

}
