using System.Globalization;
using System.Text;

namespace TextTools.Core.Plan;

/// <summary>
/// The read-only queries of the Extended Explain Plan and the parsers of their rows. Every column is selected as text
/// (numbers through TO_CHAR, so no NLS decimal or group separators) because the plug-in interface returns strings.
/// The plug-in runs them one after another (the interface cannot nest queries); a failed query just leaves its part
/// of the analysis empty.
/// </summary>
public static class PlanSql
{
    /// <summary>At most this many objects go to the dictionary queries (IN lists stay short and fast).</summary>
    public const int MaxObjects = 60;

    public static string Explain(string statementId, string sql) => $"explain plan set statement_id = '{Q(statementId)}' for {sql}";

    public static string Rows(string statementId) =>
        "select to_char(id), to_char(parent_id), to_char(position), operation, options, object_owner, object_name, object_type, " +
        "object_alias, qblock_name, to_char(cardinality), to_char(bytes), to_char(cost), access_predicates, filter_predicates, " +
        $"partition_start, partition_stop from plan_table where statement_id = '{Q(statementId)}' order by id";

    /// <summary>The adaptive plan's display map from OTHER_XML (12c+); fails harmlessly on older versions.</summary>
    public static string DisplayMap(string statementId) =>
        "select to_char(x.op), to_char(x.dis), to_char(x.par), to_char(x.skp) from plan_table p, " +
        "xmltable('/other_xml/display_map/row' passing xmltype(p.other_xml) " +
        "columns op number path '@op', dis number path '@dis', par number path '@par', skp number path '@skp') x " +
        $"where p.statement_id = '{Q(statementId)}' and p.other_xml is not null";

    public const string Version = "select version from product_component_version where product like 'Oracle%' and rownum = 1";

    /// <summary>DBMS_XPLAN text: ADVANCED (ALL + outline), +ADAPTIVE (12c+: inactive rows), +HINT_REPORT (19c+).</summary>
    public static string Text(string statementId, int majorVersion) =>
        $"select plan_table_output from table(dbms_xplan.display(null, '{Q(statementId)}', '{TextFormat(majorVersion)}'))";

    public static string TextFormat(int majorVersion) => majorVersion switch
    {
        >= 19 => "ADVANCED +ADAPTIVE +HINT_REPORT",
        >= 12 => "ADVANCED +ADAPTIVE",
        _ => "ADVANCED",
    };

    public static int MajorVersion(string? version) =>
        int.TryParse((version ?? "").Split('.')[0], NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : 0;

    /// <summary>The tables of indexes used in the plan.</summary>
    public static string? IndexTables(IEnumerable<(string Owner, string Name)> indexes) =>
        In(indexes) is { } list
            ? $"select table_owner, table_name from all_indexes where (owner, index_name) in ({list})"
            : null;

    public static string? Tables(IEnumerable<(string Owner, string Name)> tables) =>
        In(tables) is { } list
            ? "select t.owner, t.table_name, to_char(t.num_rows), to_char(t.blocks), to_char(t.last_analyzed, 'YYYY-MM-DD'), " +
              "t.temporary, t.partitioned, s.stale_stats from all_tables t left join all_tab_statistics s " +
              "on s.owner = t.owner and s.table_name = t.table_name and s.object_type = 'TABLE' " +
              $"where (t.owner, t.table_name) in ({list})"
            : null;

    public static string? Indexes(IEnumerable<(string Owner, string Name)> tables) =>
        In(tables) is { } list
            ? "select i.owner, i.index_name, i.table_owner, i.table_name, i.status, i.visibility, i.uniqueness, i.index_type, " +
              "(select listagg(c.column_name, ',') within group (order by c.column_position) from all_ind_columns c " +
              "where c.index_owner = i.owner and c.index_name = i.index_name) " +
              $"from all_indexes i where (i.table_owner, i.table_name) in ({list})"
            : null;

    /// <summary>COLUMN_EXPRESSION is a LONG: selected on its own, a failure only loses the function-based index check.</summary>
    public static string? Expressions(IEnumerable<(string Owner, string Name)> tables) =>
        In(tables) is { } list
            ? $"select index_owner, index_name, column_expression from all_ind_expressions where (table_owner, table_name) in ({list})"
            : null;

    public static string? Columns(IEnumerable<(string Owner, string Name)> tables) =>
        In(tables) is { } list
            ? $"select owner, table_name, column_name, data_type from all_tab_cols where (owner, table_name) in ({list})"
            : null;

    public static string? PartitionKeys(IEnumerable<(string Owner, string Name)> tables) =>
        In(tables) is { } list
            ? $"select owner, name, column_name from all_part_key_columns where object_type = 'TABLE' and (owner, name) in ({list})"
            : null;

    public static PlanRow? ParseRow(IReadOnlyList<string> r)
    {
        if (Fields.Int(r, 0) is not { } id) return null;
        return new PlanRow(
            id, Fields.Int(r, 1), Fields.Int(r, 2) ?? 0, Fields.At(r, 3), Fields.At(r, 4), Fields.At(r, 5), Fields.At(r, 6),
            Fields.At(r, 7), Fields.At(r, 8), Fields.At(r, 9), Fields.Long(r, 10), Fields.Long(r, 11), Fields.Long(r, 12),
            Fields.At(r, 13), Fields.At(r, 14), Fields.At(r, 15), Fields.At(r, 16));
    }

    public static DisplayMapRow? ParseDisplayMap(IReadOnlyList<string> r) =>
        Fields.Int(r, 0) is { } op && Fields.Int(r, 1) is { } dis && Fields.Int(r, 2) is { } par
            ? new DisplayMapRow(op, dis, par, Fields.Int(r, 3) == 1)
            : null;

    public static TableInfo? ParseTable(IReadOnlyList<string> r) =>
        Fields.At(r, 1).Length == 0
            ? null
            : new TableInfo(Fields.At(r, 0), Fields.At(r, 1), Fields.Long(r, 2), Fields.Long(r, 3), Fields.At(r, 4),
                string.Equals(Fields.At(r, 5), "Y", StringComparison.Ordinal), string.Equals(Fields.At(r, 6), "YES", StringComparison.Ordinal), string.Equals(Fields.At(r, 7), "YES", StringComparison.Ordinal));

    public static IndexInfo? ParseIndex(IReadOnlyList<string> r) =>
        Fields.At(r, 1).Length == 0
            ? null
            : new IndexInfo(Fields.At(r, 0), Fields.At(r, 1), Fields.At(r, 2), Fields.At(r, 3), Fields.At(r, 4), Fields.At(r, 5),
                Fields.At(r, 6), Fields.At(r, 7), Fields.At(r, 8).Split(',', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>(owner, name) pairs as an IN list of row constructors; null when there are none.</summary>
    private static string? In(IEnumerable<(string Owner, string Name)> objects)
    {
        var sb = new StringBuilder();
        foreach (var (owner, name) in objects.Where(o => o.Owner.Length > 0 && o.Name.Length > 0).Distinct().Take(MaxObjects))
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append("('").Append(Q(owner)).Append("', '").Append(Q(name)).Append("')");
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    private static string Q(string s) => s.Replace("'", "''", StringComparison.Ordinal);
}
