using TextTools.Core.Plan;

namespace TextTools.Core.Tests;

public class PlanReportTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 17, 14, 30, 0, TimeSpan.Zero);

    private static readonly string[] Plan =
    [
        "Plan hash value: 3956160932",
        "--------------------------------------------------------------------------",
        "| Id  | Operation          | Name   | Rows  | Bytes | Cost (%CPU)| Time     |",
        "|   0 | SELECT STATEMENT   |        |    14 |   532 |     3   (0)| 00:00:01 |",
        "|   1 |  MERGE JOIN CARTESIAN |     |    14 |   532 |     3   (0)| 00:00:01 |",
        "|*  2 |   TABLE ACCESS FULL| EMP    |    14 |   532 |     3   (0)| 00:00:01 |",
    ];

    [Fact]
    public void FindingsFlagFullScansAndCartesianJoinsAndAddATestNote()
    {
        var findings = PlanReport.Findings(Plan);
        Assert.Equal(3, findings.Count);
        Assert.Contains(findings, f => f.Title.StartsWith("Full table scan", StringComparison.Ordinal) && f.Detail.Contains("TABLE ACCESS FULL EMP", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Title.StartsWith("Cartesian join", StringComparison.Ordinal));
        Assert.Equal("Test finding", findings[^1].Title);
    }

    [Fact]
    public void NoPlanMeansNoFindings() => Assert.Empty(PlanReport.Findings([]));

    [Fact]
    public void HtmlHasBothTabsThePlanTheStatementAndTheFindings()
    {
        var html = PlanReport.Html("select * from emp where ename = 'O''Neil' and sal > :p_sal", Plan, null, PlanReport.Findings(Plan), At);
        Assert.Contains("id=\"t-plan\"", html, StringComparison.Ordinal);
        Assert.Contains("Findings (3)", html, StringComparison.Ordinal);
        Assert.Contains("TABLE ACCESS FULL| EMP", html, StringComparison.Ordinal);
        Assert.Contains("O&#39;&#39;Neil", html, StringComparison.Ordinal); // escaped, never raw markup
        Assert.Contains("2026-10-17 14:30:00", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"error\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlExplainsWhyThereIsNoPlan()
    {
        var html = PlanReport.Html("select 1 from dual", [], "Not connected <to> a database", [], At);
        Assert.Contains("<p class=\"error\">Not connected &lt;to&gt; a database</p>", html, StringComparison.Ordinal);
        Assert.Contains("No findings.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<pre></pre>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanCommandsAreInTheSqlMenuAndTheContextMenu()
    {
        var plans = Commands.All.OfType<PlanCommand>().ToList();
        Assert.Equal([PlanView.IdeWindow], plans.Select(p => p.View)); // the extended window is off the menu until it is stable
        Assert.All(plans, p => Assert.Contains(p.Name, Commands.ContextMenu));
    }
}
