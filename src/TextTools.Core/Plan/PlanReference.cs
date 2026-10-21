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

    private PlanReference(
        string courseUrl,
        Dictionary<string, OperationInfo> operations,
        Dictionary<string, string> columns,
        List<NoteInfo> notes,
        Dictionary<string, RuleText> rules,
        Dictionary<string, double> thresholds,
        List<long> picklerRows,
        Dictionary<string, string> banners)
    {
        CourseUrl = courseUrl;
        _operations = operations;
        Columns = columns;
        Notes = notes;
        _rules = rules;
        _thresholds = thresholds;
        PicklerDefaultRows = picklerRows;
        Banners = banners;
    }

    /// <summary>The reference embedded in this assembly.</summary>
    public static PlanReference Default { get; } = Load();

    public string CourseUrl { get; }

    public IReadOnlyDictionary<string, string> Columns { get; }

    public IReadOnlyList<NoteInfo> Notes { get; }

    public IReadOnlyList<long> PicklerDefaultRows { get; }

    public IReadOnlyDictionary<string, string> Banners { get; }

    public IReadOnlyDictionary<string, OperationInfo> Operations => _operations;

    /// <summary>"TABLE ACCESS FULL" first, then "TABLE ACCESS".</summary>
    public OperationInfo? Operation(PlanRow row) =>
        _operations.GetValueOrDefault(row.FullOperation) ?? _operations.GetValueOrDefault(row.Operation);

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
        return new PlanReference(Str(root, "courseUrl"), operations, columns, notes, rules, thresholds, pickler, banners);
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
