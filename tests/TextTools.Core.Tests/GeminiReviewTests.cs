using TextTools.Core.Sql;

namespace TextTools.Core.Tests;

/// <summary>Corner cases written by an independent review (Gemini 3.8 Flash via Antigravity, 2026-10-16).</summary>
public class GeminiReviewTests
{
    internal sealed class FakeSpecs(Dictionary<string, string> specs) : ISpecSource
    {
        public List<string> Asked { get; } = [];

        public string? PackageSpec(string? owner, string package)
        {
            Asked.Add($"{owner}.{package}");
            return specs.GetValueOrDefault(package);
        }
    }

    internal static CopyResult Run(string editor, ISpecSource? specs = null)
    {
        var selStart = editor.IndexOf('«', StringComparison.Ordinal);
        if (selStart >= 0)
        {
            var text = editor.Replace("«", "", StringComparison.Ordinal);
            var selEnd = text.IndexOf('»', StringComparison.Ordinal);
            text = text.Replace("»", "", StringComparison.Ordinal);
            return ConstantResolver.CopySql(new SqlRequest(text, selStart, selEnd - selStart, selEnd), specs);
        }
        var cursor = editor.IndexOf('§', StringComparison.Ordinal);
        return ConstantResolver.CopySql(new SqlRequest(editor.Replace("§", "", StringComparison.Ordinal), 0, 0, cursor), specs);
    }

    internal static string Copy(string editor, ISpecSource? specs = null) => Run(editor, specs).Text;

    // --- Passing tests validating correct behavior across various PL/SQL constructs ---

    [Fact]
    public void LexerAlternativeQuoteDelimitersAreHandled()
    {
        const string text = "select q'<a<b>c>', q'{x{y}z}', q'/hello/', q'#world#' from dual§;";
        Assert.Equal("select q'<a<b>c>', q'{x{y}z}', q'/hello/', q'#world#' from dual", Copy(text));
    }

    [Fact]
    public void CyrillicIdentifiersArePreservedAndResolved()
    {
        const string text = """
            declare
              константа constant number := 42;
            begin
              select константа from dual§;
            end;
            """;
        Assert.Equal("select 42 /*константа*/ from dual", Copy(text));
    }

    [Fact]
    public void NegativeDecimalAndNullLiteralsRenderCleanly()
    {
        const string text = """
            declare
              c_neg constant number := -0.75;
              c_null constant varchar2(10) := null;
            begin
              select c_neg, c_null from dual§;
            end;
            """;
        Assert.Equal("select -0.75 /*c_neg*/, null /*c_null*/ from dual", Copy(text));
    }

    [Fact]
    public void MutualCyclicPackageConstantsBecomeBindsWithoutInfiniteLoop()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal)
        {
            ["PKG_A"] = "package pkg_a is c_a constant number := pkg_b.c_b; end;",
            ["PKG_B"] = "package pkg_b is c_b constant number := pkg_a.c_a; end;",
        });
        var r = Run("select pkg_a.c_a from dual§", specs);
        Assert.Equal("select :c_a from dual", r.Text);
        Assert.Contains("constants as binds", r.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageInitializationBlockSeesBodyConstants()
    {
        const string text = """
            create or replace package body pkg is
              c_init constant varchar2(10) := 'INITIALIZED';
            begin
              select c_init from dual§;
            end pkg;
            """;
        Assert.Equal("select 'INITIALIZED' /*c_init*/ from dual", Copy(text));
    }

    [Fact]
    public void ExceptionBlockSeesEnclosingDeclarations()
    {
        const string text = """
            declare
              c_err constant number := -20001;
              l_val number;
            begin
              select 1 into l_val from dual;
            exception
              when others then
                select c_err, l_val from dual§;
            end;
            """;
        Assert.Equal("select -20001 /*c_err*/, :l_val from dual", Copy(text));
    }

    [Fact]
    public void SubprogramWithResultCacheAndDeterministicIsTracked()
    {
        const string text = """
            package body pkg is
              function calc(p_x in number) return number deterministic result_cache is
                c_mult constant number := 10;
              begin
                select p_x * c_mult from dual§;
              end;
            end pkg;
            """;
        Assert.Equal("select :p_x * 10 /*c_mult*/ from dual", Copy(text));
    }

    [Fact]
    public void SubprogramParametersInProcedureTurnIntoBinds()
    {
        const string text = """
            procedure process_order(p_order_id in number, p_status in varchar2) is
              c_active constant varchar2(1) := 'Y';
            begin
              update orders
                 set status = c_active
               where order_id = p_order_id
                 and old_status = p_status§;
            end;
            """;
        Assert.Equal("update orders\n     set status = 'Y' /*c_active*/\n   where order_id = :p_order_id\n     and old_status = :p_status",
            Copy(text).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void TriggerBodyWithDeclareSectionResolvesConstants()
    {
        const string text = """
            create or replace trigger trg_audit
            before insert or update on orders
            for each row
            declare
              c_source constant varchar2(10) := 'TRIGGER';
            begin
              select c_source from dual§;
            end;
            """;
        Assert.Equal("select 'TRIGGER' /*c_source*/ from dual", Copy(text));
    }

    [Fact]
    public void WhileLoopTracksStatementsInside()
    {
        const string text = """
            declare
              c_step constant number := 5;
              l_x number := 100;
            begin
              while l_x > 0 loop
                select l_x - c_step from dual§;
              end loop;
            end;
            """;
        Assert.Equal("select :l_x - 5 /*c_step*/ from dual", Copy(text));
    }

    [Fact]
    public void NestedSubqueryInWhereDoesNotConfoundOuterAliases()
    {
        const string text = """
            declare
              c_type constant varchar2(5) := 'RETL';
            begin
              select o.id, o.amount
                from orders o
               where o.client_id in (select c.id from clients c where c.category = c_type)§;
            end;
            """;
        Assert.Equal("select o.id, o.amount\n    from orders o\n   where o.client_id in (select c.id from clients c where c.category = 'RETL' /*c_type*/)",
            Copy(text).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ChainedConstantsAcrossTwoExternalPackagesFromSpecSource()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal)
        {
            ["PKG_A"] = "package pkg_a is c_base constant number := 100; end;",
            ["PKG_B"] = "package pkg_b is c_derived constant number := pkg_a.c_base + 50; end;",
        });
        var r = Run("select * from t where val = pkg_b.c_derived§", specs);
        Assert.Equal("select * from t where val = (100 + 50) /*pkg_b.c_derived*/", r.Text);
    }

    [Fact]
    public void MultiLineBulkCollectIntoAndMultipleReturningColumnsAreCleaned()
    {
        const string text = """
            declare
              c_id constant number := 42;
              l_a number;
              l_b varchar2(100);
            begin
              delete from t
               where id = c_id
               returning a, b
                    into l_a, l_b§;
            end;
            """;
        Assert.Equal("delete from t\n   where id = 42 /*c_id*/", Copy(text).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void MergeStatementWithComplexOnConditionPreservesAliases()
    {
        const string text = """
            declare
              c_default constant number := 0;
            begin
              merge into accounts a
              using (select id, balance from stg_accounts where status = 'READY') s
                 on (a.id = s.id)
               when matched then
                 update set a.balance = s.balance
               when not matched then
                 insert (id, balance) values (s.id, c_default)§;
            end;
            """;
        Assert.Contains("insert (id, balance) values (s.id, 0 /*c_default*/)", Copy(text), StringComparison.Ordinal);
    }

    [Fact]
    public void SequenceNextvalAndCurrvalAreNeverBoundOrTreatedAsConstants()
    {
        const string sql = "select order_seq.nextval, client_seq.currval from dual§";
        Assert.Equal("select order_seq.nextval, client_seq.currval from dual", Copy(sql));
    }

    [Fact]
    public void FunctionCallsWithMultipleArgumentsRemainUntouched()
    {
        const string sql = "select dbms_lob.substr(col, 100, 1), pkg.fn(1, 'x') from dual§";
        Assert.Equal("select dbms_lob.substr(col, 100, 1), pkg.fn(1, 'x') from dual", Copy(sql));
    }

    [Fact]
    public void StatementBetweenSlashLinesIsLocatedAccurately()
    {
        const string text = """
            select 1 from dual
            /
            select 2 from dual§
            /
            select 3 from dual
            """;
        Assert.Equal("select 2 from dual", Copy(text));
    }

    [Fact]
    public void CaseExpressionInSelectTracksEndProperly()
    {
        const string text = """
            declare
              c_status constant varchar2(1) := 'A';
            begin
              select case when status = c_status then 1 else 0 end as flag from t§;
            end;
            """;
        Assert.Equal("select case when status = 'A' /*c_status*/ then 1 else 0 end as flag from t", Copy(text));
    }

    [Fact]
    public void QualifiedTableWithSchemaAndDatabaseLinkIsPreserved()
    {
        const string sql = "select e.name from hr.employees@remote_db e where e.dept_id = pkg.c_dept§";
        var r = Run(sql);
        Assert.Equal("select e.name from hr.employees@remote_db e where e.dept_id = :c_dept", r.Text);
    }

    [Fact]
    public void SubqueryInScalarExpressionInSelectTracksTables()
    {
        const string text = """
            declare
              c_code constant varchar2(3) := 'EUR';
            begin
              select o.id, (select r.rate from rates r where r.currency = c_code) as fx_rate
                from orders o§;
            end;
            """;
        Assert.Contains("r.currency = 'EUR' /*c_code*/", Copy(text), StringComparison.Ordinal);
    }

    [Fact]
    public void CursorParameterlessTracksEnclosingConstants()
    {
        const string text = """
            declare
              c_limit constant number := 50;
              cursor c_orders is
                select id from orders where rownum <= c_limit§;
            begin
              null;
            end;
            """;
        Assert.Equal("select id from orders where rownum <= 50 /*c_limit*/", Copy(text));
    }

    [Fact]
    public void SelectionModeExtractsSubqueryWithEnclosingTablesRecognized()
    {
        const string text = """
            declare
              c_min constant number := 10;
            begin
              select * from orders o where «o.total > c_min»;
            end;
            """;
        Assert.Equal("o.total > 10 /*c_min*/", Copy(text));
    }

    [Fact]
    public void DateLiteralWithoutTimeComponentPreservesSyntax()
    {
        const string text = """
            declare
              c_d constant date := date '2026-10-16';
            begin
              select * from t where created >= c_d§;
            end;
            """;
        Assert.Equal("select * from t where created >= date '2026-10-16' /*c_d*/", Copy(text));
    }

    [Fact]
    public void TimestampLiteralPreservesExactText()
    {
        const string text = """
            declare
              c_ts constant timestamp := timestamp '2026-10-16 18:00:00.123456';
            begin
              select * from t where created >= c_ts§;
            end;
            """;
        Assert.Equal("select * from t where created >= timestamp '2026-10-16 18:00:00.123456' /*c_ts*/", Copy(text));
    }

    [Fact]
    public void InsertIntoSelectWithConstantsSubstitutesValuesInSelect()
    {
        const string text = """
            declare
              c_src constant varchar2(10) := 'ARCHIVE';
            begin
              insert into history (id, status)
              select id, c_src from orders§;
            end;
            """;
        Assert.Equal("insert into history (id, status)\n  select id, 'ARCHIVE' /*c_src*/ from orders", Copy(text).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void UnclosedStringInConstantValueThrowsFormatExceptionNotCrash()
    {
        const string text = "declare c constant varchar2(10) := 'unclosed; begin select c from dual§; end;";
        Assert.Throws<FormatException>(() => Copy(text));
    }

    [Fact]
    public void MultipleStatementsInTextResolvesCursorTargetOnly()
    {
        const string text = """
            declare
              c1 constant number := 1;
              c2 constant number := 2;
            begin
              select c1 from dual;
              select c2 from dual§;
            end;
            """;
        Assert.Equal("select 2 /*c2*/ from dual", Copy(text));
    }

    [Fact]
    public void ConstantsInInListClauseAreResolved()
    {
        const string text = """
            declare
              c_act constant char(1) := 'A';
              c_pen constant char(1) := 'P';
            begin
              select * from orders where status in (c_act, c_pen)§;
            end;
            """;
        Assert.Equal("select * from orders where status in ('A' /*c_act*/, 'P' /*c_pen*/)", Copy(text));
    }

    [Fact]
    public void DeleteStatementWithWhereResolvesConstants()
    {
        const string text = """
            declare
              c_del constant number := 999;
            begin
              delete from temp_log where session_id = c_del§;
            end;
            """;
        Assert.Equal("delete from temp_log where session_id = 999 /*c_del*/", Copy(text));
    }

    [Fact]
    public void CursorAtStartOfSelectMatchesStatement()
    {
        const string text = "§select 1 from dual;";
        Assert.Equal("select 1 from dual", Copy(text));
    }

    [Fact]
    public void CursorBeforeSemicolonMatchesStatement()
    {
        const string text = "select 1 from dual§;";
        Assert.Equal("select 1 from dual", Copy(text));
    }
}

/// <summary>Bugs found by the same review, fixed: each test was red before the fix.</summary>
public class GeminiReviewBugTests
{
    // Bug 1: Table alias following TABLE() collection expression is ignored because 'TABLE' is treated as a clause word.
    // Expected: 't' in 'FROM TABLE(...) t' is recognized as a table alias, so 't.id' stays a column reference.
    // Actual: 'TABLE' causes TableAt to return immediately; 't' is not added to _tables; 't.id' becomes ':id'.
    [Fact]
    public void TableCollectionExpressionAliasBecomesBind()
    {
        const string sql = "select t.id from table(pkg.get_rows()) t§";
        var result = GeminiReviewTests.Run(sql);
        Assert.Equal("select t.id from table(pkg.get_rows()) t", result.Text);
    }

    // Bug 2: Cursor parameters are completely skipped by PlsqlSource.DeclarationItem (via SkipItem).
    // Expected: CURSOR cur(p_id NUMBER) IS ... declares p_id as a parameter, turning 'p_id' into ':p_id' in SQL.
    // Actual: PlsqlSource skips cursor declaration entirely without inspecting parameters; 'p_id' remains an unbound identifier.
    [Fact]
    public void CursorWithParametersLeavesParametersAsUnboundIdentifiers()
    {
        const string text = """
            declare
              cursor cur(p_id in number) is
                select * from orders where order_id = p_id§;
            begin
              null;
            end;
            """;
        var result = GeminiReviewTests.Run(text);
        Assert.Equal("select * from orders where order_id = :p_id", result.Text);
    }

    // Bug 3: PlsqlSource.End returns index i + 1 after 'END <subprogram_name>;'.
    // In a package body, the enclosing unit is still in declaration mode, so '<subprogram_name>;'
    // is incorrectly parsed as a variable declaration!
    // Expected: Calls or references to the subprogram in subsequent SQL statements must not become bind variables.
    // Actual: 'helper' is recorded as a variable declaration, and 'select helper from dual' turns into 'select :helper from dual'.
    [Fact]
    public void EndWithSubprogramNameLeaksProcedureAsVariable()
    {
        const string text = """
            create or replace package body pkg is
              procedure helper is
              begin
                null;
              end helper;

              procedure run is
              begin
                select helper from dual§;
              end run;
            end pkg;
            """;
        var result = GeminiReviewTests.Run(text);
        Assert.Equal("select helper from dual", result.Text);
    }

    // Bug 4: Multi-column UPDATE SET (col1, col2) = ... target columns are not recognized by IsSetTarget.
    // Expected: Columns 'a' and 'b' in 'SET (a, b) =' are update targets and must not be replaced by PL/SQL constants.
    // Actual: IsSetTarget only checks 'SET col =' or ', col ='. 'a' in 'SET (a, b) =' is replaced with constant '10 /*a*/'.
    [Fact]
    public void MultiColumnUpdateSetOverwritesColumnNamesMatchingConstants()
    {
        const string text = """
            declare
              a constant number := 10;
            begin
              update t set (a, b) = (select 1, 2 from dual)§;
            end;
            """;
        var result = GeminiReviewTests.Run(text);
        Assert.Equal("update t set (a, b) = (select 1, 2 from dual)", result.Text);
    }

    // Bug 5: Name collision between local variable and package constant.
    // Expected: When local variable 'c_id' and unknown package constant 'pkg.c_id' appear in the same query,
    // they must produce distinct bind names (:c_id and :pkg_c_id) to avoid colliding into the same bind variable.
    // Actual: Joined does not register variable bind names into _bindNames, so BindName does not detect the collision
    // and produces :c_id for both.
    [Fact]
    public void BindNameCollisionBetweenLocalVariableAndPackageConstant()
    {
        const string text = """
            procedure p(c_id in number) is
            begin
              select c_id, pkg.c_id from dual§;
            end;
            """;
        var result = GeminiReviewTests.Run(text);
        Assert.Equal("select :c_id, :pkg_c_id from dual", result.Text);
    }

    // Bug 7: Cursor located on trailing whitespace after statement-terminating semicolon throws FormatException.
    // Expected: When the cursor is at the space after ';' (e.g. "select 1 from dual; §"), the statement is located.
    // Actual: Range checks Cursor <= until, where 'until' is the end of the ';' token, excluding trailing whitespace.
    [Fact]
    public void CursorAfterSemicolonWithTrailingSpaceFails()
    {
        const string text = "select 1 from dual; §";
        var result = GeminiReviewTests.Run(text);
        Assert.Equal("select 1 from dual", result.Text);
    }

    // Bug 9: Chained constant literal rendered with redundant parentheses.
    // Expected: If c2 := c1 and c1 := 10, resolving c2 should render the literal '10 /*c2*/'.
    // Actual: IsLiteral inspects the original tokens of c2 (which is 'c1', a Word), so it wraps in parentheses: '(10) /*c2*/'.
    [Fact]
    public void ChainedConstantLiteralRendersWithExtraParentheses()
    {
        const string text = """
            declare
              c1 constant number := 10;
              c2 constant number := c1;
            begin
              select c2 from dual§;
            end;
            """;
        var result = GeminiReviewTests.Run(text);
        Assert.Equal("select 10 /*c2*/ from dual", result.Text);
    }

    // Bug 10: LATERAL inline view alias treated as unknown package constant.
    // Expected: In 'FROM orders o, LATERAL(SELECT ...) lat', 'lat' is a table alias; 'lat.val' stays untouched.
    // Actual: 'LATERAL' is in IsClauseWord, so TableAt returns immediately without adding 'lat' to _tables.
    // 'lat.val' becomes ':val'.
    [Fact]
    public void LateralInlineViewAliasMistakenForPackageConstant()
    {
        const string sql = "select lat.val from orders o, lateral(select 1 val from dual) lat§";
        var result = GeminiReviewTests.Run(sql);
        Assert.Equal("select lat.val from orders o, lateral(select 1 val from dual) lat", result.Text);
    }

    // Bug 11: PIVOT table alias treated as unknown package constant.
    // Expected: In 'SELECT pvt.q1 FROM orders PIVOT (...) pvt', 'pvt' is a table alias; 'pvt.q1' stays untouched.
    // Actual: FindTables treats PIVOT as ending the FROM list, but does not parse the pivot alias 'pvt'.
    // 'pvt.q1' becomes ':q1'.
    [Fact]
    public void PivotTableAliasMistakenForPackageConstant()
    {
        const string sql = "select pvt.q1 from orders pivot (sum(amount) for qtr in (1 as q1)) pvt§";
        var result = GeminiReviewTests.Run(sql);
        Assert.Equal("select pvt.q1 from orders pivot (sum(amount) for qtr in (1 as q1)) pvt", result.Text);
    }

    // Bug 13: Multi-table INSERT ALL / INSERT FIRST column lists are not protected.
    // Expected: In 'INSERT ALL INTO t1 (col1) VALUES (1) ...', 'col1' is a target column list and should not be replaced.
    // Actual: FindInsertColumnLists only recognizes 'INSERT INTO', skipping 'INSERT ALL' / 'INSERT FIRST'.
    // If a constant exists with the same name, the column name is replaced with the constant value.
    [Fact]
    public void MultiTableInsertAllColumnListNotProtected()
    {
        const string text = """
            declare
              col1 constant number := 99;
            begin
              insert all
                into t1 (col1) values (1)
              select 1 from dual§;
            end;
            """;
        var result = GeminiReviewTests.Run(text);
        Assert.Contains("into t1 (col1) values", result.Text, StringComparison.Ordinal);
    }

    // Bug 14: Nested record variable fields (3-part names) mistaken for schema.package.constant.
    // Expected: In 'l_rec.sub_rec.field', 'l_rec' is a record variable, so 'l_rec.sub_rec.field' should become ':l_rec_sub_rec_field'.
    // Actual: Lookup checks InScope only for 1- and 2-part names. For 3-part names, it treats 'l_rec' as schema/owner
    // and 'sub_rec' as package name, and without a DB spec turns the last part into bind ':field'.
    [Fact]
    public void NestedRecordVariableFieldsMistakenForPackageConstant()
    {
        const string text = """
            declare
              l_user user_rec_t;
            begin
              select * from accounts where id = l_user.contact.phone§;
            end;
            """;
        var result = GeminiReviewTests.Run(text);
        Assert.Equal("select * from accounts where id = :l_user_contact_phone", result.Text);
    }
}
