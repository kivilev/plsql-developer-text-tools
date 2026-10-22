using TextTools.Core.Plan;

namespace TextTools.Core.Tests;

/// <summary>Rules on small hand-made plans: the cases a test database cannot produce on demand.</summary>
public class PlanRulesTests
{
    private static PlanRow R(int id, int? parent, string op, string options = "", string name = "", string type = "", string alias = "",
        long? rows = null, string access = "", string filter = "", string pstart = "", string pstop = "", string owner = "S") =>
        new(id, parent, 1, op, options, name.Length > 0 ? owner : "", name, type, alias, "SEL$1", rows, null, 10, access, filter, pstart, pstop);

    private static TableInfo Table(string name, long rows, long blocks, string analyzed = "2026-10-01", bool temporary = false, bool partitioned = false, bool stale = false) =>
        new("S", name, rows, blocks, analyzed, temporary, partitioned, stale);

    private static IndexInfo Index(string name, string table, string columns, string status = "VALID", string visibility = "VISIBLE", string type = "NORMAL") =>
        new("S", name, "S", table, status, visibility, "NONUNIQUE", type, columns.Split(','));

    private static PlanAnalysis Analyze(
        IReadOnlyList<PlanRow> rows,
        PlanCatalog? catalog = null,
        IReadOnlyList<string>? text = null,
        string sql = "select 1 from dual",
        IReadOnlyList<DisplayMapRow>? map = null) =>
        PlanAnalysis.Analyze(new PlanData(rows, map ?? [], text ?? [], catalog ?? PlanCatalog.Empty), sql);

    private static PlanCatalog Catalog(
        IEnumerable<TableInfo> tables,
        IEnumerable<IndexInfo>? indexes = null,
        IEnumerable<(string, string, string, string)>? columns = null,
        IEnumerable<(string, string, string)>? keys = null,
        IEnumerable<(string, string, string)>? expressions = null) =>
        new(tables, indexes ?? [], columns ?? [], keys ?? [], expressions ?? []);

    private static Finding Rule(PlanAnalysis a, string id) => Assert.Single(a.Findings, f => string.Equals(f.RuleId, id, StringComparison.Ordinal));

    [Fact]
    public void DateColumnComparedWithTimestampIsAProblemOnlyWithTheDictionary()
    {
        PlanRow[] rows =
        [
            R(0, null, "SELECT STATEMENT"),
            R(1, 0, "TABLE ACCESS", "FULL", "ORDERS", "TABLE", "\"O\"@\"SEL$1\"", 10, filter: "INTERNAL_FUNCTION(\"O\".\"CREATED\")>=:B1"),
        ];
        Assert.Equal(Level.Check, Rule(Analyze(rows), "PRED-02").Level);
        var withDates = Catalog([Table("ORDERS", 1000, 50)], columns: [("S", "ORDERS", "CREATED", "DATE")]);
        Assert.Equal(Level.Problem, Rule(Analyze(rows, withDates), "PRED-02").Level);
        var withNumbers = Catalog([Table("ORDERS", 1000, 50)], columns: [("S", "ORDERS", "CREATED", "NUMBER")]);
        Assert.DoesNotContain(Analyze(rows, withNumbers).Findings, f => f.RuleId.StartsWith("PRED", StringComparison.Ordinal));
    }

    [Fact]
    public void ConversionInAJoinConditionAndOnAColumnWithoutAnIndex()
    {
        PlanRow[] rows =
        [
            R(0, null, "SELECT STATEMENT"),
            R(1, 0, "HASH JOIN", access: "TO_NUMBER(\"B\".\"CODE\")=\"A\".\"ID\""),
            R(2, 1, "TABLE ACCESS", "FULL", "A", "TABLE", "\"A\"@\"SEL$1\"", 5),
            R(3, 1, "TABLE ACCESS", "FULL", "B", "TABLE", "\"B\"@\"SEL$1\"", 5, filter: "TO_CHAR(\"B\".\"NOTE\")='x' AND UPPER(\"B\".\"NAME\")='Y'"),
        ];
        var a = Analyze(rows, Catalog([Table("A", 5, 1), Table("B", 5, 1)], [Index("B_NAME", "B", "NAME")]));
        Assert.Equal((Level.Problem, 1), (Rule(a, "PRED-03").Level, Rule(a, "PRED-03").StepId));
        Assert.Equal(Level.Check, Rule(a, "PRED-01").Level); // NOTE has no index: the conversion costs no index here
        Assert.Contains("UPPER(\"B\".\"NAME\")", Rule(a, "PRED-04").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void FunctionBasedIndexSilencesTheFunctionRule()
    {
        PlanRow[] rows =
        [
            R(0, null, "SELECT STATEMENT"),
            R(1, 0, "TABLE ACCESS", "FULL", "B", "TABLE", "\"B\"@\"SEL$1\"", 5, filter: "UPPER(\"B\".\"NAME\")='Y'"),
        ];
        var fbi = Catalog([Table("B", 5, 1)], [Index("B_UP", "B", "SYS_NC00004$", type: "FUNCTION-BASED NORMAL")], expressions: [("S", "B_UP", "UPPER(\"NAME\")")]);
        Assert.Empty(Analyze(rows, fbi).Findings);
        Assert.Single(Analyze(rows).Findings, f => string.Equals(f.RuleId, "PRED-04", StringComparison.Ordinal)); // no dictionary: cautious "check"
    }

    [Fact]
    public void UnusableAndInvisibleIndexesOnTheFilterColumn()
    {
        PlanRow[] rows =
        [
            R(0, null, "SELECT STATEMENT"),
            R(1, 0, "TABLE ACCESS", "FULL", "T", "TABLE", "\"T\"@\"SEL$1\"", 5, filter: "\"T\".\"A\"=1 AND \"T\".\"B\"=2"),
        ];
        var c = Catalog([Table("T", 1000, 10)], [Index("T_A", "T", "A", status: "UNUSABLE"), Index("T_B", "T", "B", visibility: "INVISIBLE"), Index("T_C", "T", "C,A")]);
        var found = Analyze(rows, c).Findings.Where(f => string.Equals(f.RuleId, "ACC-08", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, found.Count);
        Assert.Contains(found, f => f.Level == Level.Problem && f.Title.Contains("T_A is UNUSABLE", StringComparison.Ordinal));
        Assert.Contains(found, f => f.Level == Level.Check && f.Title.Contains("T_B is INVISIBLE", StringComparison.Ordinal));
    }

    [Fact]
    public void FullScanShareDecidesBetweenCheckNoteAndSilence()
    {
        PlanRow[] Plan(long rows) => [R(0, null, "SELECT STATEMENT"), R(1, 0, "TABLE ACCESS", "FULL", "T", "TABLE", rows: rows)];
        var c = Catalog([Table("T", 1_000_000, 20_000)]);
        Assert.Equal(Level.Check, Rule(Analyze(Plan(100), c), "ACC-01").Level);
        Assert.Equal(Level.Info, Rule(Analyze(Plan(500_000), c), "ACC-01").Level);
        Assert.Empty(Analyze(Plan(50_000), c).Findings);
        Assert.Empty(Analyze(Plan(100)).Findings); // without the dictionary a full scan is never a finding
        Assert.Empty(Analyze(Plan(100), Catalog([Table("T", 1_000_000, 80)])).Findings); // small table
    }

    [Fact]
    public void CartesianJoinOfManyRowsAndDatabaseLinkInALoop()
    {
        PlanRow[] rows =
        [
            R(0, null, "SELECT STATEMENT"),
            R(1, 0, "NESTED LOOPS", rows: 50_000),
            R(2, 1, "MERGE JOIN", "CARTESIAN", rows: 50_000),
            R(3, 2, "TABLE ACCESS", "FULL", "A", "TABLE", rows: 500),
            R(4, 2, "BUFFER", "SORT", rows: 100),
            R(5, 4, "TABLE ACCESS", "FULL", "B", "TABLE", rows: 100),
            R(6, 1, "REMOTE", name: "R", rows: 1),
        ];
        var a = Analyze(rows);
        Assert.Equal(Level.Problem, Rule(a, "JOIN-01").Level);
        var link = Rule(a, "JOIN-04");
        Assert.Equal((Level.Problem, 6), (link.Level, link.StepId));
        Assert.Contains("~50,000 rows of step 4", link.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void NestedLoopsWithOneOuterRowOrATinyInnerTableAreFine()
    {
        PlanRow[] Plan(long outer) =>
        [
            R(0, null, "SELECT STATEMENT"),
            R(1, 0, "NESTED LOOPS"),
            R(2, 1, "TABLE ACCESS", "FULL", "A", "TABLE", rows: outer),
            R(3, 1, "TABLE ACCESS", "FULL", "B", "TABLE", rows: 1),
        ];
        Assert.Empty(Analyze(Plan(1)).Findings);
        Assert.Empty(Analyze(Plan(500), Catalog([Table("B", 10, 5), Table("A", 500, 5)])).Findings);
        Assert.Single(Analyze(Plan(500)).Findings, f => string.Equals(f.RuleId, "JOIN-02", StringComparison.Ordinal));
    }

    [Fact]
    public void AllPartitionsWithoutThePartitionKeyInThePredicates()
    {
        PlanRow[] rows =
        [
            R(0, null, "SELECT STATEMENT"),
            R(1, 0, "PARTITION RANGE", "ALL", pstart: "1", pstop: "1048575"),
            R(2, 1, "TABLE ACCESS", "FULL", "P", "TABLE", "\"P\"@\"SEL$1\"", 10, filter: "\"P\".\"STATUS\"=1"),
        ];
        var a = Analyze(rows, Catalog([Table("P", 1000, 50, partitioned: true)], keys: [("S", "P", "CREATED")]));
        Assert.Equal(Level.Info, Rule(a, "PART-01").Level);
        Assert.Empty(Analyze(rows).Findings); // the key is unknown without the dictionary
    }

    [Fact]
    public void FilterSubqueryForAFewRowsIsANote()
    {
        PlanRow[] rows =
        [
            R(0, null, "SELECT STATEMENT"),
            R(1, 0, "FILTER"),
            R(2, 1, "TABLE ACCESS", "FULL", "A", "TABLE", rows: 3),
            R(3, 1, "INDEX", "RANGE SCAN", "B_I", "INDEX", rows: 1, access: "\"B\".\"X\"=:B1"),
        ];
        Assert.Equal(Level.Info, Rule(Analyze(rows), "SUBQ-01").Level);
    }

    [Fact]
    public void NotesOfThePlanText()
    {
        PlanRow[] rows = [R(0, null, "SELECT STATEMENT"), R(1, 0, "TABLE ACCESS", "FULL", "G", "TABLE", rows: 1)];
        string[] text =
        [
            "Plan hash value: 42",
            "Note",
            "-----",
            "   - dynamic statistics used: dynamic sampling (level=2)",
            "   - SQL plan baseline \"SQL_PLAN_1\" used for this statement",
            "   - this is an adaptive plan",
            "     (rows marked '-' are inactive)",
        ];
        var a = Analyze(rows, text: text);
        Assert.Equal(Level.Check, Rule(a, "NOTE-02").Level); // no dictionary
        Assert.Equal(Level.Info, Rule(a, "NOTE-03").Level);
        Assert.Equal("this is an adaptive plan (rows marked '-' are inactive)", a.Text.Notes[2]);
        Assert.Equal(["estimate", "adaptive", "fixed"], a.Banners);
        var gtt = Analyze(rows, Catalog([Table("G", 0, 0, analyzed: "", temporary: true)]), text);
        Assert.Equal(Level.Info, Rule(gtt, "NOTE-02").Level);
        var stale = Analyze(rows, Catalog([Table("G", 0, 0, stale: true)]), text);
        Assert.Equal(Level.Problem, Rule(stale, "NOTE-02").Level);
    }

    [Fact]
    public void HintReportCodes()
    {
        string[] text =
        [
            "Hint Report (identified by operation id / Query Block Name / Object Alias):",
            "Total hints for statement: 3 (U - Unused (1), N - Unresolved (1), E - Syntax error (1))",
            "---------------------------------------------------------------------------",
            "",
            "   1 -  SEL$1",
            "         N -  qb_name(x)",
            "           -  full(t)",
            "         E -  oops",
        ];
        var t = PlanText.Parse(text);
        Assert.Equal([('N', "qb_name(x)", 1), ('E', "oops", 1)], t.Hints.Select(h => (h.Code, h.Hint, h.Step!.Value)));
        var a = Analyze([R(0, null, "SELECT STATEMENT"), R(1, 0, "FAST DUAL")], text: text);
        Assert.Contains(a.Findings, f => string.Equals(f.Title, "Hint unresolved: qb_name(x)", StringComparison.Ordinal));
    }

    [Fact]
    public void InactiveRowsFromTheTextWhenThereIsNoDisplayMap()
    {
        PlanRow[] rows =
        [
            R(0, null, "SELECT STATEMENT"),
            R(1, 0, "HASH JOIN"),
            R(2, 1, "NESTED LOOPS"),
            R(3, 2, "TABLE ACCESS", "FULL", "A", "TABLE", rows: 1),
            R(4, 2, "INDEX", "UNIQUE SCAN", "B_PK", "INDEX", rows: 1),
        ];
        var a = Analyze(rows, text: ["|-   1 |  HASH JOIN |", "|*   4 | INDEX |"]);
        Assert.Equal([0, 2, 3, 4], a.Tree.Steps.Select(s => s.Id));
        Assert.Equal(0, a.Tree.Step(2)!.Parent!.Id);
        Assert.Contains("Build side", Analyze(rows).Tree.Step(2)!.Annotations[0], StringComparison.Ordinal);
    }

    [Fact]
    public void CollectionConstructorWithTheDefaultEstimate()
    {
        PlanRow[] rows = [R(0, null, "SELECT STATEMENT"), R(1, 0, "COLLECTION ITERATOR", "CONSTRUCTOR FETCH", rows: 8168)];
        Assert.Contains("COLLECTION ITERATOR CONSTRUCTOR FETCH", Rule(Analyze(rows), "CARD-04").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void PredicateParsing()
    {
        Assert.Equal("x = '' and y = ''", Predicates.WithoutLiterals("x = 'it''s \"Q\"' and y = 'a'"));
        Assert.Equal([new ColumnRef("T", "A"), new ColumnRef("", "B")], Predicates.Columns("\"T\".\"A\"=1 AND \"B\"='\"C\"'"));
        var w = Assert.Single(Predicates.Wrapped("NVL(\"T\".\"A\",0)=1 AND TO_NUMBER(:B1)=\"X\""));
        Assert.Equal(("NVL", "NVL(\"T\".\"A\",0)"), (w.Function, w.Text));
        Assert.False(Predicates.IsJoinOfDifferentTypes("NVL(\"T\".\"A\",0)=\"T\".\"B\"", w)); // same alias: not a join
        var unclosed = Assert.Single(Predicates.Wrapped("UPPER(\"A\",(1"));
        Assert.Equal("UPPER(\"A\",(1", unclosed.Text);
    }

    [Fact]
    public void QueriesAndParsers()
    {
        Assert.Contains("'it''s'", PlanSql.Explain("it's", "select 1 from dual"), StringComparison.Ordinal);
        Assert.Contains("statement_id = 'X1'", PlanSql.Rows("X1"), StringComparison.Ordinal);
        Assert.Contains("display_map", PlanSql.DisplayMap("X1"), StringComparison.Ordinal);
        Assert.Equal("ADVANCED +ADAPTIVE +HINT_REPORT", PlanSql.TextFormat(23));
        Assert.Equal("ADVANCED +ADAPTIVE", PlanSql.TextFormat(12));
        Assert.Equal("ADVANCED", PlanSql.TextFormat(11));
        Assert.Contains("'ADVANCED'", PlanSql.Text("X1", 0), StringComparison.Ordinal);
        Assert.Equal(19, PlanSql.MajorVersion("19.0.0.0.0"));
        Assert.Equal(0, PlanSql.MajorVersion(null));
        (string, string)[] objects = [("S", "T"), ("S", "T"), ("", "X"), ("O'R", "Y")];
        foreach (var sql in new[] { PlanSql.IndexTables(objects), PlanSql.Tables(objects), PlanSql.Indexes(objects), PlanSql.Expressions(objects), PlanSql.Columns(objects), PlanSql.PartitionKeys(objects) })
        {
            Assert.NotNull(sql);
            Assert.Contains("in (('S', 'T'), ('O''R', 'Y'))", sql, StringComparison.Ordinal);
        }
        Assert.Null(PlanSql.Tables([]));
        Assert.Null(PlanSql.ParseRow(["x"]));
        Assert.Null(PlanSql.ParseDisplayMap(["1", "2"]));
        Assert.Null(PlanSql.ParseTable(["S"]));
        Assert.Null(PlanSql.ParseIndex(["S"]));
        var row = PlanSql.ParseRow(["3", "", "1", "TABLE ACCESS", "FULL", "S", "T", "TABLE", "\"T\"@\"SEL$1\"", "SEL$1", "7"])!;
        Assert.Equal((3, (int?)null, 7L, "T", "TABLE ACCESS FULL", ""), (row.Id, row.ParentId, row.Rows!.Value, row.ShortAlias, row.FullOperation, row.FilterPredicates));
    }

    [Fact]
    public void FormattingAndReference()
    {
        Assert.Equal("1,234,567", PlanFormat.Count(1_234_567));
        Assert.Equal("", PlanFormat.Count((long?)null));
        Assert.Equal("0.3%", PlanFormat.Percent(0.003));
        Assert.Equal("25%", PlanFormat.Percent(0.25));
        Assert.Equal(100, PlanFormat.LogBar(1000, 1000));
        Assert.Equal(0, PlanFormat.LogBar(null, 1000));
        Assert.Equal(0, PlanFormat.LogBar(5, 0));

        var r = PlanReference.Parse("""{"courseUrl":"u?c={rule}","thresholds":{"x":2,"picklerDefaultRows":[1,"a"]},"rules":{"A-1":{"level":"C","title":"T {a}","detail":"{b}"}},"notes":[{"match":"m","meaning":"M"}]}""");
        Assert.Equal("u?c=A-1", r.CourseLink("A-1"));
        Assert.Equal(2, r.Threshold("x", 0));
        Assert.Equal(7, r.Threshold("y", 7));
        Assert.Equal([1L], r.PicklerDefaultRows);
        Assert.Equal(Level.Check, r.Rule("A-1").Level);
        Assert.Equal(new RuleText(Level.Info, "B-2", "", ""), r.Rule("B-2"));
        Assert.Equal("M", r.Note("has m inside")!.Meaning);
        Assert.Null(r.Operation(R(1, 0, "UNKNOWN")));
        Assert.Equal("T 1 {c}", PlanReference.Fill("T {a} {c}", new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "1" }));
        Assert.Equal("open { brace", PlanReference.Fill("open { brace", new Dictionary<string, string>(StringComparer.Ordinal)));
        Assert.NotNull(PlanReference.Default.Operation(R(1, 0, "TABLE ACCESS", "FULL")));
        Assert.Equal("numbers the rows (ROWNUM)", PlanReference.Default.Operation(R(1, 0, "COUNT", "SOMETHING NEW"))!.What); // unknown options: the operation alone
    }
}
