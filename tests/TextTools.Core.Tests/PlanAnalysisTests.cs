using TextTools.Core.Plan;

namespace TextTools.Core.Tests;

/// <summary>Rules and reading order on real plans (Plans/*.txt, Oracle 23ai, schema of a performance-tuning course).</summary>
public class PlanAnalysisTests
{
    private static Finding Single(PlanAnalysis a, string rule) => Assert.Single(a.Findings, f => string.Equals(f.RuleId, rule, StringComparison.Ordinal));

    private static void None(PlanAnalysis a, string rule) => Assert.DoesNotContain(a.Findings, f => string.Equals(f.RuleId, rule, StringComparison.Ordinal));

    [Fact]
    public void AdaptivePlanShowsOnlyTheFinalRowsWithTheirFinalParents()
    {
        var a = PlanFixtures.Analyze("adaptive");
        Assert.Equal([3, 6, 10], a.Tree.Inactive.Order());
        Assert.Equal([0, 1, 2, 4, 5, 7, 8, 9], a.Tree.Steps.Select(s => s.Id));
        Assert.Equal(0, a.Tree.Step(4)!.Parent!.Id); // the HASH JOIN (3) is inactive: the nested loops hang off the statement
        Assert.Equal(5, a.Tree.Step(7)!.Parent!.Id); // the STATISTICS COLLECTOR (6) is inactive
        // Reading order: the scalar subquery (children first), then the nested loops, the statement last.
        Assert.Equal([2, 1, 7, 8, 5, 9, 4, 0], a.Tree.Steps.OrderBy(s => s.Order).Select(s => s.Id));
        Assert.Contains(a.Tree.Step(1)!.Annotations, n => n.StartsWith("Scalar subquery", StringComparison.Ordinal));
        Assert.Contains("Runs for each row of step 3 (~1 time).", a.Tree.Step(8)!.Annotations);
        Assert.Equal(["estimate", "binds", "adaptive"], a.Banners);
    }

    [Fact]
    public void AdaptivePlanFindings()
    {
        var a = PlanFixtures.Analyze("adaptive");
        var conversion = Single(a, "PRED-01");
        Assert.Equal((Level.Problem, 7), (conversion.Level, conversion.StepId));
        Assert.Contains("TO_CHAR(\"C\".\"CLIENT_ID\")", conversion.Detail, StringComparison.Ordinal);
        Assert.Contains("(NUMBER)", conversion.Detail, StringComparison.Ordinal);
        None(a, "PRED-04"); // UPPER(BLOCKED_REASON): no index on the column, so nothing to lose

        var scans = a.Findings.Where(f => string.Equals(f.RuleId, "ACC-01", StringComparison.Ordinal)).ToList();
        Assert.Equal([2, 7], scans.Select(f => f.StepId!.Value).Order());
        Assert.All(scans, f => Assert.Equal(Level.Check, f.Level));
        Assert.Contains("CLIENT_ID (CLIENT_PK)", scans.Single(f => f.StepId == 7).Detail, StringComparison.Ordinal);

        var hint = Single(a, "NOTE-01");
        Assert.Equal("Hint a syntax error: nonsense", hint.Title);
        Assert.Equal(0, hint.StepId);
        Assert.Equal(Level.Info, Single(a, "NOTE-04").Level);
        Assert.Equal(Level.Problem, a.Findings[0].Level); // problems first
    }

    [Fact]
    public void CartesianJoinWithOneRowOnOneSideIsOnlyANote()
    {
        var a = PlanFixtures.Analyze("cartesian");
        var join = Single(a, "JOIN-01");
        Assert.Equal((Level.Info, 1), (join.Level, join.StepId));
        Assert.Contains("1 x ~1,000,000", join.Detail, StringComparison.Ordinal);
        Assert.Contains(a.Findings, f => f is { RuleId: "ACC-01", StepId: 2 });
    }

    [Fact]
    public void FullScanInsideNestedLoopsIsAProblemAndNotAlsoAPlainFullScan()
    {
        var a = PlanFixtures.Analyze("nl_full_inner");
        var nl = Single(a, "JOIN-02");
        Assert.Equal((Level.Problem, 4), (nl.Level, nl.StepId));
        Assert.Contains("ACCOUNT is read in full for each of ~50 rows of step 2", nl.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(a.Findings, f => f is { RuleId: "ACC-01", StepId: 4 });
        Assert.Equal([3, 2, 4, 1, 0], a.Tree.Steps.OrderBy(s => s.Order).Select(s => s.Id));
        Assert.Contains("Runs for each row of step 2 (~50 times).", a.Tree.Step(4)!.Annotations);
        Assert.Contains("per execution", a.Describe(a.Tree.Step(4)!), StringComparison.Ordinal);
        Assert.Equal(["estimate"], a.Banners);
    }

    [Fact]
    public void FilterSubqueryForManyRows()
    {
        var a = PlanFixtures.Analyze("filter_subquery");
        var f = Single(a, "SUBQ-01");
        Assert.Equal((Level.Check, 1), (f.Level, f.StepId));
        Assert.Contains("~1,000,000 rows of step 1", f.Detail, StringComparison.Ordinal);
        Assert.Contains(a.Tree.Step(3)!.Annotations, n => n.StartsWith("Subquery: runs for each row of step 1", StringComparison.Ordinal));
    }

    [Fact]
    public void FunctionOnThePartitionKeyBreaksPruning()
    {
        var a = PlanFixtures.Analyze("partition_function");
        var p = Single(a, "PART-01");
        Assert.Equal((Level.Problem, 2), (p.Level, p.StepId));
        Assert.Contains("CREATE_DTIME", p.Detail, StringComparison.Ordinal);
        None(a, "PRED-04"); // TRUNC on a column without an index
    }

    [Fact]
    public void NumberComparedWithVarcharColumn()
    {
        var a = PlanFixtures.Analyze("implicit_conversion");
        var c = Single(a, "PRED-01");
        Assert.Equal((Level.Problem, 1), (c.Level, c.StepId));
        Assert.Contains("TO_NUMBER(\"B\".\"PASSPORT_NUMBER\")", c.Detail, StringComparison.Ordinal);
        Assert.Contains("(VARCHAR2)", c.Detail, StringComparison.Ordinal);
        var scan = Single(a, "ACC-01");
        Assert.Contains("PASSPORT_NUMBER (PASSPORT_BLACK_LIST_NUM_SER_I)", scan.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectionWithTheDefaultEstimate()
    {
        var a = PlanFixtures.Analyze("pickler");
        Assert.Equal((Level.Check, 4), (Single(a, "CARD-04").Level, Single(a, "CARD-04").StepId));
        Assert.Contains("binds", a.Banners);
    }

    [Fact]
    public void DynamicSamplingBecauseThereAreNoStatistics()
    {
        var a = PlanFixtures.Analyze("no_stats");
        var n = Single(a, "NOTE-02");
        Assert.Equal(Level.Problem, n.Level);
        Assert.Contains("RESULT", n.Detail, StringComparison.Ordinal);
        Assert.Contains("Dynamic statistics used", n.Detail, StringComparison.Ordinal);
        Assert.Null(n.StepId);
    }

    [Fact]
    public void IndexRowsDiscardedByTheTableFilter()
    {
        var a = PlanFixtures.Analyze("index_filter");
        var f = Single(a, "ACC-04");
        Assert.Equal((Level.Check, 1), (f.Level, f.StepId));
        Assert.Contains("CLIENT_DATA_FIELD_I returns ~1,000,000 rowids", f.Detail, StringComparison.Ordinal);
        Assert.Contains("FIELD_VALUE", f.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void IndexFullScanWithAFilter()
    {
        var a = PlanFixtures.Analyze("index_no_access");
        Assert.Equal(1, Single(a, "ACC-02").StepId);
    }

    [Fact]
    public void UnusedHint()
    {
        var a = PlanFixtures.Analyze("hint_unused");
        var h = Single(a, "NOTE-01");
        Assert.Equal("Hint not used: index(c no_such_index)", h.Title);
        Assert.Equal(1, h.StepId);
    }

    [Fact]
    public void LinguisticComparisonOnAnIndexedColumn()
    {
        var a = PlanFixtures.Analyze("nlssort");
        var p = Single(a, "PRED-05");
        Assert.Equal(Level.Problem, p.Level);
        Assert.Contains("C.NAME", p.Title, StringComparison.Ordinal);
        None(a, "ACC-01"); // 5 blocks: a small table is never a full scan finding
    }

    [Theory]
    [InlineData("adaptive")]
    [InlineData("cartesian")]
    [InlineData("filter_subquery")]
    [InlineData("hint_unused")]
    [InlineData("implicit_conversion")]
    [InlineData("index_filter")]
    [InlineData("index_no_access")]
    [InlineData("nl_full_inner")]
    [InlineData("nlssort")]
    [InlineData("no_stats")]
    [InlineData("partition_function")]
    [InlineData("pickler")]
    public void EveryStepIsNumberedOnceAndDescribed(string name)
    {
        var a = PlanFixtures.Analyze(name);
        Assert.Equal(Enumerable.Range(1, a.Tree.Steps.Count), a.Tree.Steps.Select(s => s.Order).Order());
        Assert.Equal(a.Tree.Steps.Count, a.Tree.Root!.Order);
        Assert.All(a.Tree.Steps, s => Assert.StartsWith($"Step {s.Order}: ", a.Describe(s), StringComparison.Ordinal));
        Assert.NotEmpty(a.Text.PlanHash);
        var html = PlanReport.Html("select 1 from dual", a, PlanFixtures.Load(name).Data.Text, null, DateTimeOffset.UnixEpoch);
        Assert.Contains("id=\"xplan\"", html, StringComparison.Ordinal);
        Assert.All(a.Tree.Steps, s => Assert.Contains($"id=\"d-{s.Id}\"", html, StringComparison.Ordinal));
    }
}
