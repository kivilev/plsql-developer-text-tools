namespace TextTools.Core.Plan;

/// <summary>A step of the final plan with its place in the reading order and what the reader should know about it.</summary>
public sealed class PlanStep
{
    internal PlanStep(PlanRow row) => Row = row;

    public PlanRow Row { get; }

    /// <summary>1-based position in the reading order (children first, then the parent).</summary>
    public int Order { get; internal set; }

    public int Depth { get; internal set; }

    public PlanStep? Parent { get; internal set; }

    public IList<PlanStep> Children { get; } = [];

    /// <summary>"Runs for each row of step 3", "Build: ..." - the exceptions that make the order meaningful.</summary>
    public IList<string> Annotations { get; } = [];

    /// <summary>The step this one is repeated for (the outer side of a nested loop, the rows a FILTER subquery checks).</summary>
    public PlanStep? RepeatedFor { get; internal set; }

    public int Id => Row.Id;
}

/// <summary>
/// The plan as a tree in the final shape: inactive rows of an adaptive plan are left out and their children moved to
/// the parents of the final plan (OTHER_XML display map; without it the "-" marks of the DBMS_XPLAN text).
/// </summary>
public sealed class PlanTree
{
    private PlanTree(IReadOnlyList<PlanStep> steps, IReadOnlySet<int> inactive)
    {
        Steps = steps;
        Inactive = inactive;
    }

    /// <summary>Steps in PLAN_TABLE order (Id), active only.</summary>
    public IReadOnlyList<PlanStep> Steps { get; }

    /// <summary>Ids of the inactive rows of an adaptive plan.</summary>
    public IReadOnlySet<int> Inactive { get; }

    public PlanStep? Root => Steps.FirstOrDefault(s => s.Parent is null);

    public PlanStep? Step(int id) => Steps.FirstOrDefault(s => s.Id == id);

    public static PlanTree Build(IReadOnlyList<PlanRow> rows, IReadOnlyList<DisplayMapRow> map, IReadOnlySet<int> textInactive)
    {
        var inactive = map.Count > 0 ? map.Where(m => m.Skipped).Select(m => m.Op).ToHashSet() : [.. textInactive];
        var opByDisplay = map.Where(m => !m.Skipped).GroupBy(m => m.Display).ToDictionary(g => g.Key, g => g.First().Op);
        var parentOf = map.ToDictionary(m => m.Op, m => m.Parent == 0 ? 0 : opByDisplay.GetValueOrDefault(m.Parent, -1));

        var steps = rows.Where(r => !inactive.Contains(r.Id)).OrderBy(r => r.Id).Select(r => new PlanStep(r)).ToList();
        var byId = steps.ToDictionary(s => s.Id);
        foreach (var step in steps)
        {
            var parentId = parentOf.TryGetValue(step.Id, out var p) ? p : step.Row.ParentId;
            // An inactive parent without a display map: climb to the nearest active ancestor.
            while (parentId is { } pid && !byId.ContainsKey(pid) && rows.FirstOrDefault(r => r.Id == pid) is { } up && up.Id != step.Id)
            {
                parentId = up.ParentId;
            }
            if (step.Id != 0 && parentId is { } id && byId.TryGetValue(id, out var parent) && parent != step)
            {
                step.Parent = parent;
                parent.Children.Add(step);
            }
        }

        var order = 0;
        foreach (var root in steps.Where(s => s.Parent is null)) Number(root, 0, ref order);
        foreach (var step in steps) Annotate(step);
        return new PlanTree(steps, inactive);
    }

    private static void Number(PlanStep step, int depth, ref int order)
    {
        step.Depth = depth;
        foreach (var child in step.Children) Number(child, depth + 1, ref order);
        step.Order = ++order;
    }

    private static void Annotate(PlanStep step)
    {
        var parent = step.Parent;
        if (parent is null) return;
        var index = parent.Children.IndexOf(step);
        var first = parent.Children[0];
        var op = parent.Row.Operation;
        if (string.Equals(op, "NESTED LOOPS", StringComparison.Ordinal) && index == 1)
        {
            step.RepeatedFor = first;
            step.Annotations.Add($"Runs for each row of step {first.Order} (~{Rows(first)} {(first.Row.Rows == 1 ? "time" : "times")}).");
        }
        else if (string.Equals(op, "FILTER", StringComparison.Ordinal) && index >= 1)
        {
            step.RepeatedFor = first;
            step.Annotations.Add($"Subquery: runs for each row of step {first.Order} (results are cached for repeated values).");
        }
        else if (string.Equals(op, "HASH JOIN", StringComparison.Ordinal) && index <= 1)
        {
            step.Annotations.Add(index == 0
                ? "Build side: the hash table is built from these rows first."
                : "Probe side: each row is looked up in the hash table.");
        }
        else if (parent.Parent is null && index < parent.Children.Count - 1 && parent.Row.Operation.EndsWith("STATEMENT", StringComparison.Ordinal))
        {
            step.Annotations.Add("Scalar subquery in the select list: runs for each row of the result.");
        }
    }

    private static string Rows(PlanStep step) => step.Row.Rows is { } r ? PlanFormat.Count(r) : "?";
}
