using System.Globalization;
using System.Text;
using TextTools.Core.Plan;

namespace TextTools.Plugin;

/// <summary>
/// Extended Explain Plan: EXPLAIN PLAN of the resolved statement in the IDE's main session (like the IDE's own F5, so
/// the user's schema and settings apply), the plan rows from PLAN_TABLE, the DBMS_XPLAN text, dictionary facts about
/// the objects of the plan, and the HTML report in an IDE HTML window. Read-only apart from the plan rows EXPLAIN PLAN
/// itself writes. The window has a fixed ID: running the command again refreshes it instead of opening another one.
/// </summary>
internal static class PlanWindow
{
    private const string WindowId = "TextTools.ExtendedPlan";
    private const int MaxPlanLines = 2000;
    private const int MaxRows = 5000;

    public static bool Show(string sql)
    {
        var (data, error) = Explain(sql);
        var analysis = data.Rows.Count > 0 ? PlanAnalysis.Analyze(data, sql) : null;
        var html = PlanReport.Html(sql, analysis, data.Text, error, DateTimeOffset.Now);
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "TextTools");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "extended-plan.html");
            File.WriteAllText(file, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            return Ide.RefreshHtml(file, WindowId, bringToFront: true) || Ide.ShowHtml(file, "", "Extended Explain Plan", WindowId);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static (PlanData Data, string? Error) Explain(string sql)
    {
        if (!Ide.Connected) return (PlanData.Empty, "Not connected to a database: connect and run the command again.");
        if (Ide.WindowIsRunning) return (PlanData.Empty, "The window is executing a statement: wait for it to finish and run the command again.");

        // A statement id of our own: PLAN_TABLE and DBMS_XPLAN show exactly this plan, whatever else is in the session's PLAN_TABLE.
        var id = "TT" + Environment.TickCount64.ToString(CultureInfo.InvariantCulture);
        if (Ide.QueryColumn(PlanSql.Explain(id, sql), 0) is null)
        {
            return (PlanData.Empty, $"EXPLAIN PLAN failed: {Ide.SqlErrorMessage()}");
        }
        var rows = Rows(PlanSql.Rows(id)).Select(PlanSql.ParseRow).OfType<PlanRow>().ToList();
        var map = Rows(PlanSql.DisplayMap(id)).Select(PlanSql.ParseDisplayMap).OfType<DisplayMapRow>().ToList();
        var major = PlanSql.MajorVersion(Ide.QuerySingleValue(PlanSql.Version));
        var text = Ide.QueryColumn(PlanSql.Text(id, major), MaxPlanLines) is { Count: > 0 } t
            ? t
            : Ide.QueryColumn(PlanSql.Text(id, 0), MaxPlanLines) ?? [];
        if (rows.Count == 0 && text.Count == 0)
        {
            return (PlanData.Empty, $"The plan could not be read: {Ide.SqlErrorMessage()}");
        }
        return (new PlanData(rows, map, text, Catalog(rows)), null);
    }

    /// <summary>Dictionary facts about the tables of the plan (directly or through their indexes); empty when the queries fail.</summary>
    private static PlanCatalog Catalog(List<PlanRow> rows)
    {
        var tables = rows.Where(r => r.IsTable).Select(r => (r.Owner, r.Name)).ToList();
        if (PlanSql.IndexTables(rows.Where(r => r.IsIndex).Select(r => (r.Owner, r.Name))) is { } indexTables)
        {
            tables.AddRange(Rows(indexTables).Where(r => r.Length >= 2).Select(r => (r[0], r[1])));
        }
        tables = [.. tables.Where(t => t.Item2.Length > 0).Distinct().Take(PlanSql.MaxObjects)];
        if (tables.Count == 0) return PlanCatalog.Empty;

        var tableInfo = Rows(PlanSql.Tables(tables)).Select(PlanSql.ParseTable).OfType<TableInfo>().ToList();
        var indexes = Rows(PlanSql.Indexes(tables)).Select(PlanSql.ParseIndex).OfType<IndexInfo>().ToList();
        var expressions = indexes.Any(i => i.FunctionBased)
            ? Rows(PlanSql.Expressions(tables)).Where(r => r.Length >= 3).Select(r => (r[0], r[1], r[2])).ToList()
            : [];
        var columns = Rows(PlanSql.Columns(tables)).Where(r => r.Length >= 4).Select(r => (r[0], r[1], r[2], r[3]));
        var keys = tableInfo.Any(t => t.Partitioned)
            ? Rows(PlanSql.PartitionKeys(tables)).Where(r => r.Length >= 3).Select(r => (r[0], r[1], r[2])).ToList()
            : [];
        return new PlanCatalog(tableInfo, indexes, columns, keys, expressions);
    }

    /// <summary>The rows of a query, or none when it is null or failed (a missing privilege only loses that part).</summary>
    private static List<string[]> Rows(string? sql) => sql is null ? [] : Ide.QueryRows(sql, MaxRows) ?? [];
}
