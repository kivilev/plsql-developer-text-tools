using System.Text.RegularExpressions;

namespace TextTools.Core.Plan;

/// <summary>P - almost always a mistake, C - check (depends on the data), I - an explanation.</summary>
public enum Level
{
    Problem,
    Check,
    Info,
}

/// <summary>A finding of a rule: the step it is about (Id, null for the whole statement) and the texts of the rule.</summary>
public sealed record Finding(string RuleId, Level Level, int? StepId, string Title, string Detail, string Normal);

/// <summary>
/// Static analysis of an explained plan: the tree in reading order, the findings of the rules and the banners.
/// Explain plan is an estimate, so findings say "expects", never "took"; a full scan alone is never a problem; the
/// dictionary (table size, statistics, indexes, column types) silences most false alarms.
/// </summary>
public sealed partial class PlanAnalysis
{
    private static readonly HashSet<string> Conversions = new(StringComparer.Ordinal)
    {
        "TO_NUMBER", "TO_CHAR", "TO_DATE", "TO_TIMESTAMP", "TO_TIMESTAMP_TZ", "SYS_OP_C2C", "TO_BINARY_DOUBLE", "TO_BINARY_FLOAT", "TO_NCHAR",
    };

    private static readonly HashSet<string> Functions = new(StringComparer.Ordinal)
    {
        "UPPER", "LOWER", "TRUNC", "SUBSTR", "NVL", "COALESCE", "DECODE", "TRIM", "LTRIM", "RTRIM", "INITCAP", "REPLACE",
    };

    private readonly PlanReference _reference;
    private readonly Dictionary<string, (string Owner, string Name)> _aliases = new(StringComparer.Ordinal);
    private readonly List<Finding> _findings = [];
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    private PlanAnalysis(PlanData data, PlanReference reference)
    {
        _reference = reference;
        Dictionary = data.Dictionary;
        Text = PlanText.Parse(data.Text);
        Tree = PlanTree.Build(data.Rows, data.DisplayMap, Text.Inactive);
        foreach (var step in Tree.Steps)
        {
            if (step.Row.ShortAlias.Length > 0 && TableOf(step) is { } t) _aliases.TryAdd(step.Row.ShortAlias, t);
        }
    }

    public PlanTree Tree { get; }

    public PlanText Text { get; }

    public PlanCatalog Dictionary { get; }

    /// <summary>Problems first, then checks, then explanations; within a level in reading order.</summary>
    public IReadOnlyList<Finding> Findings => _findings;

    /// <summary>Keys of <see cref="PlanReference.Banners"/> that apply: binds, adaptive, fixed.</summary>
    public IReadOnlyList<string> Banners { get; private set; } = [];

    public static PlanAnalysis Analyze(PlanData data, string sql, PlanReference? reference = null)
    {
        var a = new PlanAnalysis(data, reference ?? PlanReference.Default);
        a.Predicates();
        a.Access();
        a.Joins();
        a.Partitions();
        a.Subqueries();
        a.Notes();
        a.Collections();
        var order = a.Tree.Steps.ToDictionary(s => s.Id, s => s.Order);
        a._findings.Sort((x, y) => x.Level != y.Level
            ? x.Level.CompareTo(y.Level)
            : order.GetValueOrDefault(x.StepId ?? -1, 0).CompareTo(order.GetValueOrDefault(y.StepId ?? -1, 0)));
        a.Banners = a.BannersFor(sql);
        return a;
    }

    /// <summary>The table a step reads: the table itself or the table of its index.</summary>
    public (string Owner, string Name)? TableOf(PlanStep step)
    {
        var r = step.Row;
        if (r.IsTable && r.Name.Length > 0) return (r.Owner, r.Name);
        if (r.IsIndex && Dictionary.Index(r.Owner, r.Name) is { } i) return (i.TableOwner, i.TableName);
        return null;
    }

    /// <summary>The step in plain words (a template, not AI).</summary>
    public string Describe(PlanStep step)
    {
        var r = step.Row;
        var what = _reference.Operation(r)?.What is { Length: > 0 } w ? w : r.FullOperation.ToLowerInvariant();
        var text = $"Step {step.Order}: {what}{(r.Name.Length > 0 ? " " + r.Name : "")}";
        if (r.AccessPredicates.Length > 0) text += $", finding rows by {r.AccessPredicates}";
        if (r.FilterPredicates.Length > 0) text += $"; then discards rows by {r.FilterPredicates}";
        if (r.Rows is { } rows) text += $"; expects ~{PlanFormat.Count(rows)} row{(rows == 1 ? "" : "s")}{(step.RepeatedFor is null ? "" : " per execution")}";
        return text + ".";
    }

    private void Add(string rule, PlanStep? step, Dictionary<string, string> values, string? key = null)
    {
        if (!_seen.Add(rule + "|" + (key ?? step?.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""))) return;
        var t = _reference.Rule(rule);
        _findings.Add(new Finding(rule.TrimEnd('C', 'I', 'P'), t.Level, step?.Id,
            PlanReference.Fill(t.Title, values), PlanReference.Fill(t.Detail, values), PlanReference.Fill(t.Normal, values)));
    }

    private double T(string name, double fallback) => _reference.Threshold(name, fallback);

    private bool SmallTable(TableInfo? t) => t?.Blocks is { } b && b <= T("smallTableBlocks", 100);

    private (string Owner, string Name)? Resolve(ColumnRef c, PlanStep step)
    {
        if (c.Alias.Length == 0) return TableOf(step);
        return _aliases.TryGetValue(c.Alias, out var t) ? t : null;
    }

    private bool Indexed((string Owner, string Name) table, string column, bool leadingOnly = false) =>
        Dictionary.IndexesOf(table.Owner, table.Name).Any(i => leadingOnly
            ? i.Columns.Count > 0 && i.Columns[0].Equals(column, StringComparison.Ordinal)
            : i.Columns.Contains(column, StringComparer.Ordinal));

    // PRED-01..05: functions and conversions on columns.
    private void Predicates()
    {
        foreach (var step in Tree.Steps)
        {
            foreach (var predicate in new[] { step.Row.AccessPredicates, step.Row.FilterPredicates }.Where(p => p.Length > 0))
            {
                foreach (var w in Plan.Predicates.Wrapped(predicate)) Predicate(step, predicate, w);
            }
        }
    }

    private void Predicate(PlanStep step, string predicate, WrappedColumn w)
    {
        var table = Resolve(w.Column, step);
        var column = w.Column.Column;
        var known = Dictionary.Available && table is not null;
        var type = table is { } t ? Dictionary.ColumnType(t.Owner, t.Name, column) : null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["column"] = (w.Column.Alias.Length > 0 ? w.Column.Alias + "." : "") + column,
            ["call"] = w.Text,
            ["type"] = type is null ? "" : $" ({type})",
        };
        var fbi = table is { } ft && Dictionary.HasExpressionIndex(ft.Owner, ft.Name, w.Function, column);
        if (fbi) return;
        if (Conversions.Contains(w.Function))
        {
            if (Plan.Predicates.IsJoinOfDifferentTypes(predicate, w)) Add("PRED-03", step, values, w.Text);
            else if (known && !Indexed(table!.Value, column)) AddLevel("PRED-01", Level.Check, step, values, w.Text);
            else Add("PRED-01", step, values, w.Text);
        }
        else if (string.Equals(w.Function, "INTERNAL_FUNCTION", StringComparison.Ordinal))
        {
            if (type is null) Add("PRED-02C", step, values, w.Text);
            else if (type.Equals("DATE", StringComparison.Ordinal)) Add("PRED-02", step, values, w.Text);
        }
        else if (string.Equals(w.Function, "NLSSORT", StringComparison.Ordinal))
        {
            if (!known || Indexed(table!.Value, column)) Add("PRED-05", step, values, w.Text);
        }
        else if (Functions.Contains(w.Function) && (!known || Indexed(table!.Value, column)))
        {
            Add("PRED-04", step, values, w.Text);
        }
    }

    /// <summary>A rule finding at another level than the rule's default (e.g. no index on the column: P becomes C).</summary>
    private void AddLevel(string rule, Level level, PlanStep step, Dictionary<string, string> values, string key)
    {
        var before = _findings.Count;
        Add(rule, step, values, key);
        if (_findings.Count > before) _findings[^1] = _findings[^1] with { Level = level };
    }

    // ACC-01, ACC-02, ACC-04, ACC-08: access methods.
    private void Access()
    {
        foreach (var step in Tree.Steps)
        {
            var r = step.Row;
            if (string.Equals(r.Operation, "TABLE ACCESS", StringComparison.Ordinal) && r.Options.Contains("FULL", StringComparison.Ordinal) && !InsideNestedLoops(step)) FullScan(step);
            if (IndexReadWithoutAccess(r))
            {
                Add("ACC-02", step, new(StringComparer.Ordinal) { ["index"] = r.Name });
            }
            if (string.Equals(r.Operation, "TABLE ACCESS", StringComparison.Ordinal) && r.Options.StartsWith("BY", StringComparison.Ordinal) && r.FilterPredicates.Length > 0) IndexFilter(step);
            UnusableIndexes(step);
        }
    }

    /// <summary>
    /// A range scan with a filter only, or a full scan with a filter: the whole index (or a large part) is read and
    /// filtered. For a non-leading column a full scan shows the same condition as access and filter.
    /// </summary>
    private static bool IndexReadWithoutAccess(PlanRow r)
    {
        if (!string.Equals(r.Operation, "INDEX", StringComparison.Ordinal) || r.FilterPredicates.Length == 0) return false;
        if (r.Options.StartsWith("RANGE SCAN", StringComparison.Ordinal)) return r.AccessPredicates.Length == 0;
        return r.Options.StartsWith("FULL SCAN", StringComparison.Ordinal) && !r.Options.Contains("MIN/MAX", StringComparison.Ordinal);
    }

    private static bool InsideNestedLoops(PlanStep step) =>
        step.Parent is { } p && string.Equals(p.Row.Operation, "NESTED LOOPS", StringComparison.Ordinal) && p.Children.IndexOf(step) == 1;

    private void FullScan(PlanStep step)
    {
        if (TableOf(step) is not { } table || Dictionary.Table(table.Owner, table.Name) is not { } info) return;
        if (SmallTable(info) || info.NumRows is not { } numRows || numRows <= 0 || step.Row.Rows is not { } rows) return;
        var share = (double)rows / numRows;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["table"] = table.Name,
            ["numRows"] = PlanFormat.Count(numRows),
            ["blocks"] = PlanFormat.Count(info.Blocks),
            ["rows"] = PlanFormat.Count(rows),
            ["share"] = PlanFormat.Percent(share),
            ["indexed"] = IndexedFilterColumns(step, table),
        };
        if (share <= T("fullScanSmallShare", 0.01)) Add("ACC-01", step, values);
        else if (share >= T("fullScanLargeShare", 0.1)) Add("ACC-01I", step, values);
    }

    /// <summary>" Indexes lead with filter columns: STATUS (IX_ORDERS_STATUS)." or "".</summary>
    private string IndexedFilterColumns(PlanStep step, (string Owner, string Name) table)
    {
        var columns = Plan.Predicates.Columns(step.Row.FilterPredicates)
            .Where(c => Resolve(c, step) == table).Select(c => c.Column).Distinct(StringComparer.Ordinal).ToList();
        var hits = Dictionary.IndexesOf(table.Owner, table.Name)
            .Where(i => i.Columns.Count > 0 && columns.Contains(i.Columns[0], StringComparer.Ordinal) && !i.Unusable && !i.Invisible)
            .Select(i => $"{i.Columns[0]} ({i.Name})").ToList();
        return hits.Count == 0 ? "" : $" Indexes lead with filter columns: {string.Join(", ", hits)}.";
    }

    private void IndexFilter(PlanStep step)
    {
        var index = step.Children.FirstOrDefault(c => string.Equals(c.Row.Operation, "INDEX", StringComparison.Ordinal));
        if (index is null && step.Parent is { } p && string.Equals(p.Row.Operation, "NESTED LOOPS", StringComparison.Ordinal) && p.Children.IndexOf(step) == 1
            && p.Children[0] is { Row.Operation: "NESTED LOOPS" } inner && inner.Children.Count > 1 && string.Equals(inner.Children[1].Row.Operation, "INDEX", StringComparison.Ordinal))
        {
            index = inner.Children[1]; // NL batching: the index is the inner side of the nested loop below
        }
        if (index?.Row.Rows is not { } indexRows || step.Row.Rows is not { } rows) return;
        if (indexRows < T("indexFilterMinRows", 100) || rows * T("indexFilterRatio", 10) > indexRows) return;
        var table = TableOf(step);
        var columns = Plan.Predicates.Columns(step.Row.FilterPredicates).Where(c => table is null || Resolve(c, step) == table)
            .Select(c => c.Column).Distinct(StringComparer.Ordinal);
        Add("ACC-04", step, new(StringComparer.Ordinal)
        {
            ["index"] = index.Row.Name,
            ["indexRows"] = PlanFormat.Count(indexRows),
            ["rows"] = PlanFormat.Count(rows),
            ["columns"] = string.Join(", ", columns),
        });
    }

    private void UnusableIndexes(PlanStep step)
    {
        if (TableOf(step) is not { } table) return;
        var columns = Plan.Predicates.Columns(step.Row.AccessPredicates + " " + step.Row.FilterPredicates)
            .Where(c => Resolve(c, step) == table).Select(c => c.Column).ToHashSet(StringComparer.Ordinal);
        foreach (var i in Dictionary.IndexesOf(table.Owner, table.Name).Where(i => (i.Unusable || i.Invisible) && i.Columns.Count > 0 && columns.Contains(i.Columns[0])))
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["index"] = i.Name,
                ["state"] = i.Unusable ? "UNUSABLE" : "INVISIBLE",
                ["columns"] = string.Join(", ", i.Columns),
            };
            if (i.Unusable) Add("ACC-08", step, values, i.Name);
            else AddLevel("ACC-08", Level.Check, step, values, i.Name);
        }
    }

    // JOIN-01, JOIN-02, JOIN-04.
    private void Joins()
    {
        foreach (var step in Tree.Steps.Where(s => s.Children.Count >= 2))
        {
            var r = step.Row;
            var outer = step.Children[0];
            var inner = step.Children[1];
            if (string.Equals(r.FullOperation, "MERGE JOIN CARTESIAN", StringComparison.Ordinal))
            {
                var values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["left"] = PlanFormat.Count(outer.Row.Rows),
                    ["right"] = PlanFormat.Count(inner.Row.Rows),
                };
                var bothMany = outer.Row.Rows > 1 && inner.Row.Rows > 1;
                Add(bothMany && r.Rows >= T("cartesianSmallRows", 200) ? "JOIN-01" : "JOIN-01I", step, values);
            }
            if (!string.Equals(r.Operation, "NESTED LOOPS", StringComparison.Ordinal) || outer.Row.Rows is not { } outerRows) continue;
            var loop = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["outer"] = PlanFormat.Count(outerRows),
                ["outerStep"] = outer.Order.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["table"] = inner.Row.Name,
            };
            if (string.Equals(inner.Row.Operation, "TABLE ACCESS", StringComparison.Ordinal) && inner.Row.Options.Contains("FULL", StringComparison.Ordinal)
                && outerRows > T("nlOuterRowsSafe", 1)
                && !(TableOf(inner) is { } t && SmallTable(Dictionary.Table(t.Owner, t.Name))))
            {
                Add("JOIN-02", inner, loop);
            }
            if (Subtree(inner).Any(s => string.Equals(s.Row.Operation, "REMOTE", StringComparison.Ordinal)) && outerRows > T("remoteOuterRowsSafe", 2)) Add("JOIN-04", inner, loop);
        }
    }

    private static IEnumerable<PlanStep> Subtree(PlanStep step) => step.Children.SelectMany(Subtree).Prepend(step);

    // PART-01.
    private void Partitions()
    {
        foreach (var step in Tree.Steps.Where(s => s.Row.Operation.StartsWith("PARTITION ", StringComparison.Ordinal)
                                                   && s.Row.Options.Contains("ALL", StringComparison.Ordinal)))
        {
            var target = Subtree(step).Skip(1).FirstOrDefault(s => TableOf(s) is not null);
            if (target is null || TableOf(target) is not { } table) continue;
            var key = Dictionary.PartitionKey(table.Owner, table.Name);
            if (key.Count == 0) continue;
            var used = Subtree(step).SelectMany(s => Plan.Predicates.Columns(s.Row.AccessPredicates + " " + s.Row.FilterPredicates)
                    .Where(c => Resolve(c, s) == table))
                .Any(c => key.Contains(c.Column, StringComparer.Ordinal));
            Add(used ? "PART-01" : "PART-01I", step, new(StringComparer.Ordinal) { ["table"] = table.Name, ["key"] = string.Join(", ", key) });
        }
    }

    // SUBQ-01.
    private void Subqueries()
    {
        foreach (var step in Tree.Steps.Where(s => string.Equals(s.Row.Operation, "FILTER", StringComparison.Ordinal) && s.Children.Count >= 2))
        {
            var outer = step.Children[0];
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["step"] = Order(step),
                ["subStep"] = Order(step.Children[1]),
                ["outerStep"] = Order(outer),
                ["outer"] = PlanFormat.Count(outer.Row.Rows) is { Length: > 0 } c ? c : "?",
            };
            Add(outer.Row.Rows is { } n && n <= T("filterOuterRowsSmall", 10) ? "SUBQ-01I" : "SUBQ-01", step, values);
        }
    }

    private static string Order(PlanStep s) => s.Order.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // NOTE-01..04.
    private void Notes()
    {
        foreach (var h in Text.Hints)
        {
            var meaning = h.Code switch
            {
                'E' => "a syntax error",
                'N' => "unresolved",
                _ => "not used",
            };
            Add("NOTE-01", h.Step is { } id ? Tree.Step(id) : null,
                new(StringComparer.Ordinal) { ["meaning"] = meaning, ["hint"] = h.Hint, ["reason"] = h.Reason.Length > 0 ? $" ({h.Reason})" : "" },
                h.Code + h.Hint);
        }
        foreach (var note in Text.Notes)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["note"] = Sentence(note) };
            if (note.Contains("dynamic statistics used", StringComparison.OrdinalIgnoreCase)
                || note.Contains("dynamic sampling used", StringComparison.OrdinalIgnoreCase))
            {
                DynamicSampling(values);
            }
            else if (note.Contains("SQL plan baseline", StringComparison.OrdinalIgnoreCase) || note.Contains("SQL profile", StringComparison.OrdinalIgnoreCase)
                     || note.Contains("SQL patch", StringComparison.OrdinalIgnoreCase))
            {
                Add("NOTE-03", null, values, note);
            }
            else if (note.Contains("adaptive plan", StringComparison.OrdinalIgnoreCase))
            {
                Add("NOTE-04", null, values, note);
            }
        }
    }

    private void DynamicSampling(Dictionary<string, string> values)
    {
        if (!Dictionary.Available)
        {
            Add("NOTE-02", null, values);
            return;
        }
        var tables = Tree.Steps.Select(TableOf).OfType<(string Owner, string Name)>().Distinct()
            .Select(t => Dictionary.Table(t.Owner, t.Name)).OfType<TableInfo>().ToList();
        var bad = tables.Where(t => !t.Temporary && (t.LastAnalyzed.Length == 0 || t.Stale)).Select(t => t.Name).ToList();
        if (bad.Count > 0)
        {
            values["tables"] = string.Join(", ", bad);
            Add("NOTE-02P", null, values);
        }
        else
        {
            Add("NOTE-02I", null, values);
        }
    }

    private static string Sentence(string note)
    {
        var s = note.Trim();
        if (s.Length == 0) return s;
        var end = s.EndsWith('.') ? "" : ".";
        return char.ToUpperInvariant(s[0]) + s[1..] + end;
    }

    // CARD-04.
    private void Collections()
    {
        foreach (var step in Tree.Steps.Where(s => string.Equals(s.Row.Operation, "COLLECTION ITERATOR", StringComparison.Ordinal)))
        {
            if (step.Row.Rows is { } rows && _reference.PicklerDefaultRows.Contains(rows))
            {
                Add("CARD-04", step, new(StringComparer.Ordinal)
                {
                    ["object"] = step.Row.Name.Length > 0 ? step.Row.Name : step.Row.FullOperation,
                    ["rows"] = PlanFormat.Count(rows),
                });
            }
        }
    }

    private List<string> BannersFor(string sql)
    {
        var banners = new List<string> { "estimate" };
        if (BindPattern().IsMatch(Plan.Predicates.WithoutLiterals(sql))) banners.Add("binds");
        if (Tree.Inactive.Count > 0 || Text.Notes.Any(n => n.Contains("adaptive plan", StringComparison.OrdinalIgnoreCase))) banners.Add("adaptive");
        if (Text.Notes.Any(n => n.Contains("SQL plan baseline", StringComparison.OrdinalIgnoreCase)
                                || n.Contains("SQL profile", StringComparison.OrdinalIgnoreCase) || n.Contains("SQL patch", StringComparison.OrdinalIgnoreCase)))
        {
            banners.Add("fixed");
        }
        return banners;
    }

    [GeneratedRegex(@"(?<![\w:]):[A-Za-z][\w$#]*", RegexOptions.None, Limits.RegexTimeoutMs)]
    private static partial Regex BindPattern();
}
