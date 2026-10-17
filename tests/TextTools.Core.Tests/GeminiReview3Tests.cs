using System.Globalization;
using TextTools.Core.Sql;

namespace TextTools.Core.Tests;

/// <summary>
/// Independent review 3 tests covering database lookups, package variables, bind collisions, and SQL edge cases.
/// </summary>
/// <summary>Corner cases from the third independent review (Gemini 3.8 Flash, 2026-10-17): DB lookups, package variables, binds.</summary>
public class GeminiReview3Tests
{
    internal sealed class FakeSpecs(Dictionary<string, string> specs) : ISpecSource
    {
        public List<string> Asked { get; } = [];

        public string? PackageSpec(string? owner, string package)
        {
            var key = owner is null ? package : $"{owner}.{package}";
            Asked.Add($"{owner}.{package}");
            return specs.GetValueOrDefault(key) ?? specs.GetValueOrDefault(package);
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

    // --- 1. Database lookups & ISpecSource integration ---

    [Fact]
    public void SpecOf_ResolvedConstantFromQualifiedPackageInSpecSource()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal)
        {
            ["HR.PKG"] = "package pkg is c_rate constant number := 1.25; end;",
        });
        var result = Run("select * from emp where rate > hr.pkg.c_rate§", specs);
        Assert.Equal("select * from emp where rate > 1.25 /*hr.pkg.c_rate*/", result.Text);
        Assert.Equal(["HR.PKG"], specs.Asked);
    }

    [Fact]
    public void SpecOf_FunctionCallWithParentheses_NeverQueriesDatabase()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal)
        {
            ["PKG"] = "package pkg is c_x constant number := 10; end;",
        });
        var result = Run("select hr.pkg.f(1), pkg.f(c_x) from dual§", specs);
        Assert.Equal("select hr.pkg.f(1), pkg.f(c_x) from dual", result.Text);
        Assert.Empty(specs.Asked);
    }

    [Fact]
    public void SpecOf_SynonymPointsToPackageWithDifferentName_ResolvesConstants()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal)
        {
            ["SYN_CONST"] = "package real_pkg is c_tax constant number := 0.20; end;",
        });
        var result = Run("select syn_const.c_tax from dual§", specs);
        Assert.Equal("select 0.20 /*syn_const.c_tax*/ from dual", result.Text);
        Assert.Equal([".SYN_CONST"], specs.Asked);
    }

    [Fact]
    public void SpecOf_ExceedingMaxSpecLookups_StopsQueryingAndEmitsBinds()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal));
        var parts = new List<string>();
        for (var i = 1; i <= 52; i++)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"pkg_{i}.c_val"));
        }
        var sql = "select " + string.Join(", ", parts) + " from dual§";
        var result = Run(sql, specs);
        Assert.Equal(50, specs.Asked.Count);
        Assert.Contains(":pkg_52_c_val", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void SpecOf_BarePackageMemberWithoutDatabase_ConvertsToBind()
    {
        var result = Run("select pkg_unknown.c_limit from dual§", null);
        Assert.Equal("select :c_limit from dual", result.Text);
        Assert.Contains("constants as binds: pkg_unknown.c_limit", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void SpecOf_BarePackageFunctionWithoutDatabase_ConvertsToBind()
    {
        var result = Run("select pkg_unknown.no_parens_func from dual§", null);
        Assert.Equal("select :no_parens_func from dual", result.Text);
        Assert.Contains("constants as binds: pkg_unknown.no_parens_func", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void SpecOf_BarePackageFunctionWithDatabase_PreservesFunctionCall()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal)
        {
            ["PKG_FUNC"] = "package pkg_func is function get_time return date; end;",
        });
        var result = Run("select pkg_func.get_time from dual§", specs);
        Assert.Equal("select pkg_func.get_time from dual", result.Text);
        Assert.Equal("no constants found", result.Summary);
    }

    [Fact]
    public void SpecOf_SequenceNextvalAndCurrval_NeverQueryDatabase()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal));
        var result = Run("select my_seq.nextval, hr.my_seq.currval from dual§", specs);
        Assert.Equal("select my_seq.nextval, hr.my_seq.currval from dual", result.Text);
        Assert.Empty(specs.Asked);
    }

    [Fact]
    public void SpecOf_TableAliasMatchingPackageName_NeverQueriesDatabase()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal));
        var result = Run("select pkg.id from orders pkg where pkg.status = 1§", specs);
        Assert.Equal("select pkg.id from orders pkg where pkg.status = 1", result.Text);
        Assert.Empty(specs.Asked);
    }

    [Fact]
    public void SpecOf_NegativeLookupCachedWithinSameRun()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal));
        var result = Run("select pkg.c_a, pkg.c_b from dual§", specs);
        Assert.Equal("select :c_a, :c_b from dual", result.Text);
        Assert.Single(specs.Asked);
    }

    [Fact]
    public void SpecOf_PackageLevelVariableInDatabaseSpec_ResolvesLiteral()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal)
        {
            ["CONST_PKG"] = "package const_pkg is g_max pls_integer := 5000; end;",
        });
        var result = Run("select * from t where rownum <= const_pkg.g_max§", specs);
        Assert.Equal("select * from t where rownum <= 5000 /*const_pkg.g_max*/", result.Text);
    }

    // --- 2. SpecQuery tests ---

    [Fact]
    public void SpecQuery_SourceGeneratesUserSourceWhenOwnerIsNull()
    {
        var sql = SpecQuery.Source(null, "MY_PKG");
        Assert.Equal("select text from user_source where name = 'MY_PKG' and type = 'PACKAGE' order by line", sql);
    }

    [Fact]
    public void SpecQuery_SourceGeneratesAllSourceWhenOwnerIsSpecified()
    {
        var sql = SpecQuery.Source("HR", "MY_PKG");
        Assert.Equal("select text from all_source where owner = 'HR' and name = 'MY_PKG' and type = 'PACKAGE' order by line", sql);
    }

    [Fact]
    public void SpecQuery_SynonymGeneratesCurrentSchemaAndPublicPriority()
    {
        var sql = SpecQuery.Synonym("SYN_NAME");
        Assert.Contains("where synonym_name = 'SYN_NAME'", sql, StringComparison.Ordinal);
        Assert.Contains("sys_context('USERENV', 'CURRENT_SCHEMA')", sql, StringComparison.Ordinal);
        Assert.Contains("order by case owner when 'PUBLIC' then 2 else 1 end", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SpecQuery_ParseTarget_ValidAndInvalidTargets()
    {
        Assert.Equal(("HR", "PKG"), SpecQuery.ParseTarget("HR.PKG"));
        Assert.Equal(("SCOTT", "ORDERS_PKG"), SpecQuery.ParseTarget("SCOTT.ORDERS_PKG"));
        Assert.Null(SpecQuery.ParseTarget(null));
        Assert.Null(SpecQuery.ParseTarget(""));
        Assert.Null(SpecQuery.ParseTarget("NO_DOT"));
        Assert.Null(SpecQuery.ParseTarget(".START_DOT"));
        Assert.Null(SpecQuery.ParseTarget("END_DOT."));
    }

    // --- 3. Package variables vs constants & scoping ---

    [Fact]
    public void PackageLevelVariable_SingleVariableReference_ResolvesChainedValue()
    {
        const string text = """
            create or replace package pkg is
              c_base constant number := 10;
              g_derived number := c_base;
            end;
            /
            procedure test_p is
            begin
              select pkg.g_derived from dual§;
            end;
            """;
        Assert.Equal("select 10 /*pkg.g_derived*/ from dual", Copy(text));
    }

    [Fact]
    public void PackageLevelVariable_NonLiteralExpression_NotTrustedAndBecomesBind()
    {
        const string text = """
            create or replace package pkg is
              g_expr number := 10 + 20;
            end;
            /
            procedure test_p is
            begin
              select pkg.g_expr from dual§;
            end;
            """;
        var result = Run(text);
        Assert.Equal("select :g_expr from dual", result.Text);
        Assert.Contains("1 bind variable", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageLevelVariable_RecordConstructor_NotTrustedAndBecomesBind()
    {
        const string text = """
            create or replace package pkg is
              g_rec t_rec := t_rec(1, 'TEST');
            end;
            /
            procedure test_p is
            begin
              select pkg.g_rec from dual§;
            end;
            """;
        Assert.Equal("select :g_rec from dual", Copy(text));
    }

    [Fact]
    public void PackageLevelVariable_NotNullAndDefault_ResolvesLiteral()
    {
        const string text = """
            create or replace package pkg is
              g_def number not null default 42;
            end;
            /
            procedure test_p is
            begin
              select pkg.g_def from dual§;
            end;
            """;
        Assert.Equal("select 42 /*pkg.g_def*/ from dual", Copy(text));
    }

    [Fact]
    public void PackageLevelVariable_Subtype_ResolvesLiteral()
    {
        const string text = """
            create or replace package pkg is
              subtype code_t is varchar2(3);
              g_code code_t := 'USA';
            end;
            /
            procedure test_p is
            begin
              select pkg.g_code from dual§;
            end;
            """;
        Assert.Equal("select 'USA' /*pkg.g_code*/ from dual", Copy(text));
    }

    [Fact]
    public void LocalVariableInProcedure_WithInitialValue_NeverTreatedAsConstant()
    {
        const string text = """
            procedure test_local is
              l_var number := 999;
            begin
              select l_var from dual§;
            end;
            """;
        Assert.Equal("select :l_var from dual", Copy(text));
    }

    [Fact]
    public void LocalConstantInProcedure_WithInitialValue_ResolvesLiteral()
    {
        const string text = """
            procedure test_local is
              c_var constant number := 999;
            begin
              select c_var from dual§;
            end;
            """;
        Assert.Equal("select 999 /*c_var*/ from dual", Copy(text));
    }

    [Fact]
    public void PackageBodyGlobal_WithInitialValue_ResolvesInsideProcedures()
    {
        const string text = """
            create or replace package body pkg is
              g_body_val number := 123;
              procedure run is
              begin
                select g_body_val from dual§;
              end;
            end pkg;
            """;
        Assert.Equal("select 123 /*g_body_val*/ from dual", Copy(text));
    }

    [Fact]
    public void ConstantInsideNestedProcedure_ScopedToNestedProcedureOnly()
    {
        const string text = """
            create or replace package body pkg is
              procedure outer_p is
                procedure inner_p is
                  c_nested constant number := 77;
                begin
                  select c_nested from dual§;
                end;
              begin
                null;
              end;
            end pkg;
            """;
        Assert.Equal("select 77 /*c_nested*/ from dual", Copy(text));
    }

    [Fact]
    public void DeclarationsAfterProcedureForwardDeclaration_ParsedCorrectly()
    {
        const string text = """
            create or replace package body pkg is
              procedure helper(p in number);
              c_after_fwd constant number := 42;
              procedure helper(p in number) is
              begin
                select c_after_fwd from dual§;
              end;
            end pkg;
            """;
        Assert.Equal("select 42 /*c_after_fwd*/ from dual", Copy(text));
    }

    [Fact]
    public void DeclarationsAfterProcedureImplementation_ParsedCorrectly()
    {
        const string text = """
            create or replace package body pkg is
              procedure first_p is
              begin
                null;
              end first_p;

              c_between constant number := 55;

              procedure second_p is
              begin
                select c_between from dual§;
              end second_p;
            end pkg;
            """;
        Assert.Equal("select 55 /*c_between*/ from dual", Copy(text));
    }

    [Fact]
    public void BooleanConstant_NeverPutIntoSql_BecomesBind()
    {
        const string text = """
            declare
              c_flag constant boolean := true;
            begin
              select c_flag from dual§;
            end;
            """;
        var result = Run(text);
        Assert.Equal("select :c_flag from dual", result.Text);
        Assert.Contains("constants as binds: c_flag", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void TypePercentTypeVariable_WithoutValue_BecomesBind()
    {
        const string text = """
            procedure run is
              l_row accounts%rowtype;
            begin
              select l_row.balance from dual§;
            end;
            """;
        Assert.Equal("select :l_row_balance from dual", Copy(text));
    }

    // --- 4. Unknown constants and bind name collisions ---

    [Fact]
    public void UnknownConstant_SameConstantUsedMultipleTimes_UsesSameBind()
    {
        var result = Run("select pkg.c_limit, pkg.c_limit from dual§", null);
        Assert.Equal("select :c_limit, :c_limit from dual", result.Text);
        Assert.Contains("constants as binds: pkg.c_limit", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownConstant_DifferentPackagesWithSameConstantName_QualifiesSecond()
    {
        var result = Run("select pkg1.c_id, pkg2.c_id from dual§", null);
        Assert.Equal("select :c_id, :pkg2_c_id from dual", result.Text);
    }

    [Fact]
    public void UnknownConstant_ThreePartName_Colliding_QualifiesFullSchema()
    {
        var result = Run("select hr.pkg1.c_code, scott.pkg1.c_code from dual§", null);
        Assert.Equal("select :c_code, :scott_pkg1_c_code from dual", result.Text);
    }

    [Fact]
    public void UnknownConstant_QuotedIdentifier_TrimsQuotes()
    {
        var result = Run("select \"pkg\".\"c_my_val\" from dual§", null);
        Assert.Equal("select :c_my_val from dual", result.Text);
    }
}

/// <summary>
/// Tests documenting real bugs discovered during review 3. Each test is expected to fail against
/// the current code and includes a comment explaining the expected vs actual behavior.
/// </summary>
/// <summary>Bugs found by the third independent review (Gemini 3.8 Flash, 2026-10-17), fixed: each test was red before.</summary>
public class GeminiReview3BugTests
{
    // Bug 1: XMLTABLE table alias in FROM clause is not recognized as a table alias.
    // Expected: 'xt' is a table alias produced by XMLTABLE(...); 'xt.id' is a column and should remain 'xt.id'.
    // Actual: TableAt only checks 'TABLE', 'LATERAL', 'ONLY', so XMLTABLE is not recognized as a table function.
    // Alias 'xt' is omitted from _tables, causing 'xt.id' to be mistaken for an unknown package constant and turned into ':id'.
    [Fact]
    public void XmlTableAlias_TreatedAsUnknownPackageConstant()
    {
        const string sql = "select xt.id from xmltable('/rows/row' passing xml columns id number path '@id') xt§";
        var result = GeminiReview3Tests.Run(sql);
        Assert.Equal("select xt.id from xmltable('/rows/row' passing xml columns id number path '@id') xt", result.Text);
    }

    // Bug 2: JSON_TABLE table alias in FROM clause is not recognized as a table alias.
    // Expected: 'jt' is a table alias produced by JSON_TABLE(...); 'jt.val' is a column and should remain 'jt.val'.
    // Actual: TableAt misses 'JSON_TABLE', omitting alias 'jt' from _tables and turning column 'jt.val' into ':val'.
    [Fact]
    public void JsonTableAlias_TreatedAsUnknownPackageConstant()
    {
        const string sql = "select jt.val from json_table(doc, '$[*]' columns (val varchar2(10) path '$.val')) jt§";
        var result = GeminiReview3Tests.Run(sql);
        Assert.Equal("select jt.val from json_table(doc, '$[*]' columns (val varchar2(10) path '$.val')) jt", result.Text);
    }

    // Bug 3: CROSS APPLY table alias is not recognized in FindTables.
    // Expected: In Oracle 12c+ 'CROSS APPLY (...) dt', 'dt' is a table alias; 'dt.amount' should remain 'dt.amount'.
    // Actual: FindTables only looks for 'JOIN' keywords and lacks handling for 'APPLY'. Alias 'dt' is omitted from
    // _tables, causing 'dt.amount' to be mistaken for an unknown package constant and replaced with ':amount'.
    [Fact]
    public void CrossApplyTableAlias_TreatedAsUnknownPackageConstant()
    {
        const string sql = "select dt.amount from orders o cross apply (select * from items i where i.order_id = o.id) dt§";
        var result = GeminiReview3Tests.Run(sql);
        Assert.Equal("select dt.amount from orders o cross apply (select * from items i where i.order_id = o.id) dt", result.Text);
    }

    // Bug 4: OUTER APPLY table alias is not recognized in FindTables.
    // Expected: In 'OUTER APPLY (...) dt', 'dt' is an applied subquery alias; 'dt.val' should remain 'dt.val'.
    // Actual: FindTables ignores 'OUTER APPLY', omitting 'dt' from _tables and converting 'dt.val' to ':val'.
    [Fact]
    public void OuterApplyTableAlias_TreatedAsUnknownPackageConstant()
    {
        const string sql = "select dt.val from orders o outer apply (select 1 val from dual) dt§";
        var result = GeminiReview3Tests.Run(sql);
        Assert.Equal("select dt.val from orders o outer apply (select 1 val from dual) dt", result.Text);
    }

    // Bug 5: DELETE without FROM clause omits table alias from _tables.
    // Expected: Oracle allows 'DELETE table_name alias WHERE ...' without the FROM keyword; 'o.id' is a column.
    // Actual: FindTables switch handles 'UPDATE', 'INTO', 'USING', but lacks 'DELETE'.
    // When FROM is omitted, 'o' is never added to _tables, and 'o.id' is converted into bind ':id'.
    [Fact]
    public void DeleteWithoutFromClause_TableAliasOmittedFromTables()
    {
        const string sql = "delete orders o where o.id = 1§";
        var result = GeminiReview3Tests.Run(sql);
        Assert.Equal("delete orders o where o.id = 1", result.Text);
    }

    // Bug 6: Unknown package constant appearing before a local variable causes bind collision.
    // Expected: Package constant 'pkg.c_val' and local variable 'c_val' are distinct identifiers; they must not collide.
    // E.g., 'select :pkg_c_val, :c_val from dual' or each bound distinctly.
    // Actual: When 'pkg.c_val' comes first, BindName claims ':c_val'. When variable 'c_val' is evaluated, Substitute
    // calls Joined directly without checking _bindNames, also returning ':c_val'. Both collide into 'select :c_val, :c_val from dual'.
    [Fact]
    public void UnknownConstant_PrecedingVariable_CausesBindNameCollision()
    {
        const string text = """
            declare
              c_val number;
            begin
              select pkg.c_val, c_val from dual§;
            end;
            """;
        var result = GeminiReview3Tests.Run(text);
        Assert.NotEqual("select :c_val, :c_val from dual", result.Text);
    }

    // Bug 7: Schema-qualified package lookup SpecOf matches local window package without checking owner.
    // Expected: 'remote_owner.pkg.c_val' refers to package 'pkg' under schema 'remote_owner', and should be looked up
    // via ISpecSource (resolving to 999).
    // Actual: SpecOf checks '_window.Specs.Contains(package)' without verifying 'owner is null'.
    // Bug 8: ANSI date literal is corrupted when a local variable named 'date' is in scope.
    // Expected: In 'select date '2026-01-01' from dual', 'date '...'' is an ANSI date literal; 'date' should not be replaced.
    // Actual: Substitute checks InScope for 'date', finds variable 'date', and replaces it with ':date', producing
    // invalid SQL: 'select :date '2026-01-01' from dual'.
    [Fact]
    public void DateLiteral_CorruptedWhenVariableNamedDateInScope()
    {
        const string text = """
            declare
              date varchar2(10) := '2026-01-01';
            begin
              select date '2026-01-01' from dual§;
            end;
            """;
        var result = GeminiReview3Tests.Run(text);
        Assert.Equal("select date '2026-01-01' from dual", result.Text);
    }
}
