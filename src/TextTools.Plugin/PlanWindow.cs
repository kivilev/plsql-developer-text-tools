using System.Globalization;
using System.Text;
using TextTools.Core.Plan;

namespace TextTools.Plugin;

/// <summary>
/// Extended Explain Plan: EXPLAIN PLAN of the resolved statement in the IDE's main session (like the IDE's own F5, so
/// the user's schema and settings apply), DBMS_XPLAN.DISPLAY of it, and the HTML report (Plan and Findings tabs) in an
/// IDE HTML window. The window has a fixed ID: running the command again refreshes it instead of opening another one.
/// </summary>
internal static class PlanWindow
{
    private const string WindowId = "TextTools.ExtendedPlan";
    private const int MaxPlanLines = 2000;

    public static bool Show(string sql)
    {
        var (plan, error) = Explain(sql);
        var html = PlanReport.Html(sql, plan, error, PlanReport.Findings(plan), DateTimeOffset.Now);
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

    private static (IReadOnlyList<string> Plan, string? Error) Explain(string sql)
    {
        if (!Ide.Connected) return ([], "Not connected to a database: connect and run the command again.");
        if (Ide.WindowIsRunning) return ([], "The window is executing a statement: wait for it to finish and run the command again.");

        // A statement id of our own: DBMS_XPLAN shows exactly this plan, whatever else is in the session's PLAN_TABLE.
        var id = "TT" + Environment.TickCount64.ToString(CultureInfo.InvariantCulture);
        if (Ide.QueryColumn($"explain plan set statement_id = '{id}' for {sql}", 0) is null)
        {
            return ([], $"EXPLAIN PLAN failed: {Ide.SqlErrorMessage()}");
        }
        var plan = Ide.QueryColumn($"select plan_table_output from table(dbms_xplan.display(null, '{id}', 'TYPICAL'))", MaxPlanLines);
        return plan is { Count: > 0 } ? (plan, null) : ([], $"DBMS_XPLAN.DISPLAY returned nothing: {Ide.SqlErrorMessage()}");
    }
}
