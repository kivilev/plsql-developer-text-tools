namespace TextTools.Core.Plan;

/// <summary>ALL_TABLES + ALL_TAB_STATISTICS of a table in the plan.</summary>
public sealed record TableInfo(
    string Owner,
    string Name,
    long? NumRows,
    long? Blocks,
    string LastAnalyzed,
    bool Temporary,
    bool Partitioned,
    bool Stale);

/// <summary>ALL_INDEXES + ALL_IND_COLUMNS (column names in key order; SYS_NC...$ for an expression).</summary>
public sealed record IndexInfo(
    string Owner,
    string Name,
    string TableOwner,
    string TableName,
    string Status,
    string Visibility,
    string Uniqueness,
    string IndexType,
    IReadOnlyList<string> Columns)
{
    public bool Unusable => Status.Equals("UNUSABLE", StringComparison.Ordinal);
    public bool Invisible => Visibility.Equals("INVISIBLE", StringComparison.Ordinal);
    public bool FunctionBased => IndexType.StartsWith("FUNCTION-BASED", StringComparison.Ordinal);
}

/// <summary>
/// Dictionary facts about the objects of a plan. They turn rules from guesses into findings and, more often, silence
/// false alarms: a small table, an index that does not exist, fresh statistics. Empty when the queries failed
/// (no connection, no privileges): rules then work on the plan alone and stay cautious.
/// </summary>
public sealed class PlanCatalog
{
    private readonly Dictionary<(string, string), TableInfo> _tables;
    private readonly Dictionary<(string, string), IndexInfo> _indexes;
    private readonly Dictionary<(string, string), string> _columnTypes;
    private readonly Dictionary<(string, string), IReadOnlyList<string>> _partitionKeys;

    public PlanCatalog(
        IEnumerable<TableInfo> tables,
        IEnumerable<IndexInfo> indexes,
        IEnumerable<(string Owner, string Table, string Column, string Type)> columns,
        IEnumerable<(string Owner, string Table, string Column)> partitionKeys,
        IEnumerable<(string Owner, string Index, string Expression)> expressions)
    {
        _tables = tables.GroupBy(t => (t.Owner, t.Name)).ToDictionary(g => g.Key, g => g.First());
        _indexes = indexes.GroupBy(i => (i.Owner, i.Name)).ToDictionary(g => g.Key, g => g.First());
        _columnTypes = columns.GroupBy(c => (c.Owner, c.Table + "." + c.Column)).ToDictionary(g => g.Key, g => g.First().Type);
        _partitionKeys = partitionKeys.GroupBy(k => (k.Owner, k.Table))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(k => k.Column)]);
        Expressions = [.. expressions];
        Available = _tables.Count > 0;
    }

    public static PlanCatalog Empty { get; } = new([], [], [], [], []);

    /// <summary>The dictionary was read: at least one table of the plan is known.</summary>
    public bool Available { get; }

    /// <summary>ALL_IND_EXPRESSIONS: (index owner, index name, expression text).</summary>
    public IReadOnlyList<(string Owner, string Index, string Expression)> Expressions { get; }

    public TableInfo? Table(string owner, string name) => _tables.GetValueOrDefault((owner, name));

    public IndexInfo? Index(string owner, string name) => _indexes.GetValueOrDefault((owner, name));

    /// <summary>The indexes of a table.</summary>
    public IEnumerable<IndexInfo> IndexesOf(string owner, string table) =>
        _indexes.Values.Where(i => i.TableOwner.Equals(owner, StringComparison.Ordinal) && i.TableName.Equals(table, StringComparison.Ordinal));

    /// <summary>DATA_TYPE of a column ("NUMBER", "VARCHAR2", "TIMESTAMP(6)"), or null when unknown.</summary>
    public string? ColumnType(string owner, string table, string column) => _columnTypes.GetValueOrDefault((owner, table + "." + column));

    public IReadOnlyList<string> PartitionKey(string owner, string table) => _partitionKeys.GetValueOrDefault((owner, table)) ?? [];

    /// <summary>A function-based index of the table whose expression mentions both the function and the column.</summary>
    public bool HasExpressionIndex(string owner, string table, string function, string column) =>
        IndexesOf(owner, table).Where(i => i.FunctionBased).Any(i => Expressions.Any(e =>
            e.Owner.Equals(i.Owner, StringComparison.Ordinal) && e.Index.Equals(i.Name, StringComparison.Ordinal)
            && e.Expression.Contains(function, StringComparison.OrdinalIgnoreCase)
            && e.Expression.Contains(column, StringComparison.OrdinalIgnoreCase)));
}
