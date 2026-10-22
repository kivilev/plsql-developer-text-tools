using System.Globalization;

namespace TextTools.Core.Plan;

/// <summary>One row of PLAN_TABLE (only the columns the analysis uses). Numbers are null when the column is empty.</summary>
public sealed record PlanRow(
    int Id,
    int? ParentId,
    int Position,
    string Operation,
    string Options,
    string Owner,
    string Name,
    string ObjectType,
    string Alias,
    string QueryBlock,
    long? Rows,
    long? Bytes,
    long? Cost,
    string AccessPredicates,
    string FilterPredicates,
    string PartitionStart,
    string PartitionStop)
{
    /// <summary>"TABLE ACCESS FULL", "INDEX RANGE SCAN", "HASH JOIN".</summary>
    public string FullOperation => Options.Length == 0 ? Operation : $"{Operation} {Options}";

    /// <summary>The alias without its query block: <c>"C"@"SEL$1"</c> -> <c>C</c>; empty when there is none.</summary>
    public string ShortAlias
    {
        get
        {
            var at = Alias.IndexOf('@', StringComparison.Ordinal);
            return (at < 0 ? Alias : Alias[..at]).Trim('"');
        }
    }

    public bool IsTable => ObjectType.StartsWith("TABLE", StringComparison.Ordinal);
    public bool IsIndex => ObjectType.StartsWith("INDEX", StringComparison.Ordinal);
}

/// <summary>A row of the adaptive plan's display map (OTHER_XML): which rows are skipped and who is the parent in the final plan.</summary>
/// <param name="Op">PLAN_TABLE.ID.</param>
/// <param name="Display">The row's number in the displayed (final) plan.</param>
/// <param name="Parent">The parent's display number.</param>
/// <param name="Skipped">An inactive row of the adaptive plan.</param>
public sealed record DisplayMapRow(int Op, int Display, int Parent, bool Skipped);

/// <summary>Everything the plug-in read from the database for one explained statement.</summary>
public sealed record PlanData(
    IReadOnlyList<PlanRow> Rows,
    IReadOnlyList<DisplayMapRow> DisplayMap,
    IReadOnlyList<string> Text,
    PlanCatalog Dictionary)
{
    public static PlanData Empty { get; } = new([], [], [], PlanCatalog.Empty);
}

/// <summary>Parses the string rows that SQL_Field returns (every column is selected as text by <see cref="PlanSql"/>).</summary>
internal static class Fields
{
    public static string At(IReadOnlyList<string> row, int i) => i < row.Count ? row[i] : "";

    public static long? Long(IReadOnlyList<string> row, int i) =>
        long.TryParse(At(row, i), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) ? v : null;

    public static int? Int(IReadOnlyList<string> row, int i) =>
        int.TryParse(At(row, i), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) ? v : null;
}
