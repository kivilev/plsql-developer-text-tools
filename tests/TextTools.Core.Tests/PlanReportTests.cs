using TextTools.Core.Plan;

namespace TextTools.Core.Tests;

public class PlanReportTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 17, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void HtmlHasTheTabsTheStepPanelsTheFindingsAndTheText()
    {
        var (sql, data) = PlanFixtures.Load("implicit_conversion");
        var a = PlanAnalysis.Analyze(data, sql);
        var html = PlanReport.Html("select * from t where name = 'O''Neil' and sal > :p_sal", a, data.Text, null, At);
        foreach (var tab in new[] { "t-plan", "t-findings", "t-text", "t-reference" })
        {
            Assert.Contains($"id=\"{tab}\"", html, StringComparison.Ordinal);
        }
        Assert.Contains($"Findings ({a.Findings.Count})", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"P\">&#9679; 1 problems</span>", html, StringComparison.Ordinal);
        Assert.Contains("access: ", html.Replace("filter: ", "access: ", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("filter: TO_NUMBER(&quot;B&quot;.&quot;PASSPORT_NUMBER&quot;)=123456", html, StringComparison.Ordinal);
        Assert.Contains("<u>PASSPORT_NUMBER</u>", html, StringComparison.Ordinal); // predicate column of the index underlined
        Assert.Contains("utm_content=PRED-01", html, StringComparison.Ordinal); // one course button per finding
        Assert.Contains("O&#39;&#39;Neil", html, StringComparison.Ordinal); // escaped, never raw markup
        Assert.Contains("onclick=\"return copyText()\"", html, StringComparison.Ordinal);
        Assert.Contains("TABLE ACCESS FULL", html, StringComparison.Ordinal);
        Assert.Contains("Operations in this plan", html, StringComparison.Ordinal);
        Assert.Contains("2026-10-17 14:30:00", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"error\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlShowsBannersNotesAndStatistics()
    {
        var (sql, data) = PlanFixtures.Load("no_stats");
        var html = PlanReport.Html(sql, PlanAnalysis.Analyze(data, sql), data.Text, null, At);
        Assert.Contains("Notes of this plan", html, StringComparison.Ordinal);
        Assert.Contains("no statistics", html, StringComparison.Ordinal);
        Assert.Contains("No indexes.", html, StringComparison.Ordinal);
        var adaptive = PlanFixtures.Load("adaptive");
        var html2 = PlanReport.Html(adaptive.Sql, PlanAnalysis.Analyze(adaptive.Data, adaptive.Sql), adaptive.Data.Text, null, At);
        Assert.Contains("class=\"banner\">The statement has bind variables", html2, StringComparison.Ordinal);
        Assert.Contains("&times;1", html2, StringComparison.Ordinal); // the inner side of nested loops
        Assert.Contains("<b>WALLET_CLIENT_UNQ</b>", html2, StringComparison.Ordinal); // the index used by the step
    }

    [Fact]
    public void HtmlExplainsWhyThereIsNoPlan()
    {
        var html = PlanReport.Html("select 1 from dual", null, [], "Not connected <to> a database", At);
        Assert.Contains("<p class=\"error\">Not connected &lt;to&gt; a database</p>", html, StringComparison.Ordinal);
        Assert.Contains("No findings.", html, StringComparison.Ordinal);
        Assert.Contains("No plan text.", html, StringComparison.Ordinal);
        Assert.Contains("init(null)", html, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanCommandsAreInTheSqlMenuAndTheContextMenu()
    {
        var plans = Commands.All.OfType<PlanCommand>().ToList();
        Assert.Equal([PlanView.IdeWindow, PlanView.Extended], plans.Select(p => p.View));
        Assert.All(plans, p => Assert.Contains(p.Name, Commands.ContextMenu));
    }
}
