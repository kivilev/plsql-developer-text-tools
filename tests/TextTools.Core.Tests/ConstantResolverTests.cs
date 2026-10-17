using TextTools.Core.Sql;

namespace TextTools.Core.Tests;

/// <summary>Editor texts use '§' for the cursor and '«...»' for the selection.</summary>
public class ConstantResolverTests
{
    private sealed class FakeSpecs(Dictionary<string, string> specs) : ISpecSource
    {
        public List<string> Asked { get; } = [];

        public string? PackageSpec(string? owner, string package)
        {
            Asked.Add($"{owner}.{package}");
            return specs.GetValueOrDefault(package);
        }
    }

    private static CopyResult Run(string editor, ISpecSource? specs = null)
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

    private static string Copy(string editor, ISpecSource? specs = null) => Run(editor, specs).Text;

    [Fact]
    public void LocalConstantBecomesItsValueWithTheNameInAComment()
    {
        const string text = """
            declare
              c_new constant varchar2(3) := 'NEW';
              c_max constant pls_integer default 100;
            begin
              for r in (select * from orders where status = §c_new and rownum <= c_max) loop
                null;
              end loop;
            end;
            """;
        Assert.Equal("select * from orders where status = 'NEW' /*c_new*/ and rownum <= 100 /*c_max*/", Copy(text));
        Assert.Equal("2 constants", Run(text).Summary);
    }

    [Fact]
    public void IntoIsRemovedAndVariablesBecomeBinds()
    {
        const string text = """
            procedure p(p_id in number, p_rec in t%rowtype) is
              c_active constant char(1) := 'A';
              l_name varchar2(100);
            begin
              select name
                into l_name
                from clients
               where id = p_id and state = c_active and region = p_rec.region and name like '%' || c_active;§
            end;
            """;
        Assert.Equal("select name\n    from clients\n   where id = :p_id and state = 'A' /*c_active*/ and region = :p_rec_region and name like '%' || 'A' /*c_active*/",
            Copy(text).ReplaceLineEndings("\n"));
        Assert.Equal("2 constants, 2 bind variables", Run(text).Summary);
    }

    [Fact]
    public void BulkCollectIntoAndReturningIntoAreRemoved()
    {
        Assert.Equal("select id from t where x = 1 /*c*/",
            Copy("declare c constant number := 1; l ids; begin select id bulk collect into l from t where x = c§; end;"));
        Assert.Equal("update t set x = 1 /*c*/",
            Copy("declare c constant number := 1; l number; begin §update t set x = c returning id into l; end;"));
        Assert.Equal("insert into t(id) values (1 /*c*/)",
            Copy("declare c constant number := 1; begin §insert into t(id) values (c); end;"));
    }

    [Fact]
    public void InnermostDeclarationWinsAndVariablesShadowConstants()
    {
        const string text = """
            package body pkg is
              c_x constant number := 1;
              c_y constant number := 2;
              procedure p is
                c_x constant number := 10;
                c_y number := 20;
              begin
                select c_x, c_y from dual§;
              end;
            end;
            """;
        Assert.Equal("select 10 /*c_x*/, :c_y from dual", Copy(text));
    }

    [Fact]
    public void PackageSpecConstantsInTheSameTextAreVisibleInTheBodyAndQualified()
    {
        const string text = """
            create or replace package pkg_const is
              c_type_retail constant number := 10;
              c_prefix constant varchar2(10) := 'R_';
              c_name constant varchar2(20) := c_prefix || 'MAIN';
              procedure run;
            end pkg_const;
            /
            create or replace package body pkg_const is
              procedure run is
              begin
                delete from orders where type_id = c_type_retail and name = pkg_const.c_name and code = hr.pkg_const.c_prefix§;
              end;
            end pkg_const;
            /
            """;
        Assert.Equal("delete from orders where type_id = 10 /*c_type_retail*/ and name = ('R_' || 'MAIN') /*pkg_const.c_name*/"
                     + " and code = 'R_' /*hr.pkg_const.c_prefix*/", Copy(text));
    }

    [Fact]
    public void OtherPackagesComeFromTheSpecSource()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal)
        {
            ["PKG_CONST"] = "package pkg_const is\n  c_limit constant number := 1000;\n  c_start constant date := date '2020-01-01';\n  c_neg constant number := -1;\n" +
                            "  c_flag constant boolean := true;\n  c_now constant date := sysdate;\nend;",
        });
        var result = Run("select * from t where n < pkg_const.c_limit and d > pkg_const.c_start and m = pkg_const.c_neg " +
                         "and f = pkg_const.c_flag and x = pkg_const.c_missing and w = pkg_const.c_now§", specs);
        Assert.Equal("select * from t where n < 1000 /*pkg_const.c_limit*/ and d > date '2020-01-01' /*pkg_const.c_start*/ " +
                     "and m = -1 /*pkg_const.c_neg*/ and f = :c_flag and x = pkg_const.c_missing and w = sysdate /*pkg_const.c_now*/",
            result.Text);
        Assert.Equal("4 constants, 1 bind variable; constants as binds: pkg_const.c_flag", result.Summary);
        Assert.Equal([".PKG_CONST"], specs.Asked); // asked once, cached; tables and aliases are never asked
    }

    [Fact]
    public void PackageMembersFromTheDatabaseThatAreNotConstants()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal)
        {
            ["UTIL"] = "package util is\n  g_state varchar2(10);\n  function today return date;\nend;",
        });
        var r = Run("select * from t where s = util.g_state and d = util.today and x = other.c_x§", specs);
        Assert.Equal("select * from t where s = :util_g_state and d = util.today and x = :c_x", r.Text);
        Assert.Equal("2 bind variables; constants as binds: other.c_x", r.Summary);
    }

    [Fact]
    public void WithoutTheSpecificationQualifiedNamesBecomeBindsExceptTablesAliasesSequencesAndCalls()
    {
        const string sql = "select o.status, q.n, hr.orders.id, seq.nextval, pkg.f(1), s.c_x from hr.orders o"
                           + " join (select 1 n from dual) q on q.n = o.id, items@dblink.corp i where i.id = pkg_const.c_new and o.x = pkg_const.c_new§";
        var r = Run(sql);
        Assert.Equal("select o.status, q.n, hr.orders.id, seq.nextval, pkg.f(1), :c_x from hr.orders o"
                     + " join (select 1 n from dual) q on q.n = o.id, items@dblink.corp i where i.id = :c_new and o.x = :c_new", r.Text);
        Assert.Equal("3 bind variables; constants as binds: s.c_x, pkg_const.c_new", r.Summary);
    }

    [Fact]
    public void LoopRecordFieldsAreVariablesInsideTheLoopOnly() =>
        Assert.Equal("update t set a = :rec_v where id = :rec_id",
            Copy("begin for rec in (select v, id from s) loop update t set a = rec.v where id = rec.id§; end loop; end;"));

    [Fact]
    public void LoopRecordIsNotVisibleAfterTheLoop() =>
        Assert.Equal("delete from t where a = :v",
            Copy("begin for rec in c loop null; end loop; delete from t where a = rec.v§; end;"));

    [Fact]
    public void SameConstantNameFromDifferentPackagesGetsQualifiedBinds() =>
        Assert.Equal("select :c_x, :b_c_x, :c_x from dual", Copy("select a.c_x, b.c_x, a.c_x from dual§"));

    [Fact]
    public void SelectionKnowsTheAliasesOfItsStatement() =>
        Assert.Equal("o.status = :c_new", Copy("begin select count(*) into l from orders o where «o.status = pkg.c_new»; end;"));

    [Fact]
    public void DmlTablesAreNotConstants() =>
        Assert.Equal("update hr.t x set x.a = :c_a where x.b in (select y.b from s.u y)",
            Copy("update hr.t x set x.a = pkg.c_a where x.b in (select y.b from s.u y)§"));

    [Fact]
    public void OwnPackageSpecFromTheSpecSourceForABodyWindow()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal) { ["PKG"] = "package pkg as c_one constant number := 1; end;" });
        Assert.Equal("select 1 /*c_one*/ from dual",
            Copy("package body pkg as procedure p is begin select c_one from dual§; end; end;", specs));
    }

    [Theory]
    [InlineData("declare c constant number := c; begin select c from dual§; end;")]
    [InlineData("declare l number; c constant number := l + 1; begin select c from dual§; end;")]
    [InlineData("declare c constant boolean := false; begin select c from dual§; end;")]
    public void ConstantsUnusableInSqlBecomeBindsNamedLikeTheConstant(string text)
    {
        var r = Run(text);
        Assert.Equal("select :c from dual", r.Text);
        Assert.Equal("1 bind variable; constants as binds: c", r.Summary);
    }

    [Fact]
    public void ConstantCalledLikeAFunctionIsReported()
    {
        var r = Run("declare c constant number := f(1); begin select c(1) from dual§; end;");
        Assert.Equal("select c(1) from dual", r.Text);
        Assert.Equal("no constants found; not resolved: c", r.Summary);
    }

    [Fact]
    public void CollectionElementIsReportedNotBound()
    {
        var r = Run("declare l_tab t_tab; begin select * from t where id = l_tab(1)§; end;");
        Assert.Equal("select * from t where id = l_tab(1)", r.Text);
        Assert.Equal("no constants found; not resolved: l_tab", r.Summary);
    }

    [Fact]
    public void NamesInStringsCommentsAliasesAndNamedArgumentsAreNotTouched()
    {
        const string text = """
            declare
              c constant number := 1;
            begin
              select 'c' as c, f(c => 2), /* c */ t.c, :c, q'[c]' -- c
                from t§;
            end;
            """;
        Assert.Equal("select 'c' as c, f(c => 2), /* c */ t.c, :c, q'[c]' -- c\n    from t", Copy(text).ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData("update t set version = version + 1, x = 2 where y = version§", "update t set version = version + 1, x = 2 where y = version")]
    [InlineData("insert into s.t a (version, x) values (version, 1)§", "insert into s.t a (version, x) values ('1.0' /*version*/, 1)")]
    [InlineData("insert into t(version) select version from dual§", "insert into t(version) select '1.0' /*version*/ from dual")]
    [InlineData("insert into t (select version from dual)§", "insert into t (select '1.0' /*version*/ from dual)")]
    [InlineData("merge into t using s on (t.id = s.id) when not matched then insert (version) values (version)§",
        "merge into t using s on (t.id = s.id) when not matched then insert (version) values ('1.0' /*version*/)")]
    public void ColumnsNamedLikeAConstantAreNotReplaced(string sql, string expected) =>
        Assert.Equal(expected, Copy($"declare version constant varchar2(10) := '1.0'; begin {sql}; end;"));

    [Fact]
    public void QuotedNamesAreCaseSensitiveUnquotedAreNot()
    {
        Assert.Equal("select 1 /*C_X*/, 2 /*\"c_y\"*/, \"C_Y\" from dual",
            Copy("declare c_x constant number := 1; \"c_y\" constant number := 2; begin select C_X, \"c_y\", \"C_Y\" from dual§; end;"));
    }

    [Fact]
    public void SelectionIsCopiedAsSelected()
    {
        Assert.Equal("status = 'A' /*c_a*/",
            Copy("declare c_a constant char(1) := 'A'; begin update t set x = 1 where «status = c_a»; end;"));
    }

    [Theory]
    [InlineData("begin\n  l_tab.delete;\n  §null;\nend;")]
    [InlineData("§")]
    [InlineData("")]
    [InlineData("-- only a comment§")]
    [InlineData("create trigger trg before insert or update of x on t for each row begin §null; end;")]
    [InlineData("grant select, insert on t to u§;")]
    public void NoStatementAtTheCursorIsAReadableError(string text)
    {
        var e = Assert.Throws<FormatException>(() => Run(text));
        Assert.Contains("SQL statement", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectionOutsideTheTextIsAReadableError() =>
        Assert.Throws<FormatException>(() => ConstantResolver.CopySql(new SqlRequest("select 1 from dual", 10, 50, 0)));

    [Fact]
    public void WhitespaceOnlySelectionIsAReadableError() =>
        Assert.Throws<FormatException>(() => Run("select 1 from dual«   »"));

    [Theory]
    [InlineData("select 1 from dual;\nselect 2 from dual§;\nselect 3 from dual;", "select 2 from dual")]
    [InlineData("select 1 from dual\n/\nselect 2 from dual§\n/\n", "select 2 from dual")]
    [InlineData("select 1 from dual;§\nselect 2 from dual;", "select 1 from dual")]
    [InlineData("begin\n  <<lbl>> §select 1 into l from dual;\nend;", "select 1 from dual")]
    [InlineData("begin\n  open c for select 1 from dual§;\nend;", "select 1 from dual")]
    [InlineData("declare cursor c is select a from t where x = 1§; begin null; end;", "select a from t where x = 1")]
    [InlineData("begin if x then delete from t§; else update t set a = 1; end if; end;", "delete from t")]
    [InlineData("begin forall i in 1 .. l.count insert into t values l(i)§; end;", "insert into t values l(i)")]
    [InlineData("with a as (select 1 x from dual) select x into l from a§;", "with a as (select 1 x from dual) select x from a")]
    [InlineData("merge into t using s on (t.id = s.id) when matched then update set t.x = s.x§;", "merge into t using s on (t.id = s.id) when matched then update set t.x = s.x")]
    [InlineData("select case when 1 = 1 then 'a' end x from dual§", "select case when 1 = 1 then 'a' end x from dual")]
    [InlineData("select 1.5e3, .5, 1..2, n'x', nq'{a'b}', 'it''s' from dual§", "select 1.5e3, .5, 1..2, n'x', nq'{a'b}', 'it''s' from dual")]
    public void FindsTheStatementUnderTheCursor(string text, string expected) => Assert.Equal(expected, Copy(text));

    [Fact]
    public void BlockStructureIsTrackedThroughIfLoopCaseAndNestedUnits()
    {
        const string text = """
            create or replace package body pkg is
              c_g constant number := 1;
              type t_rec is record (a number, b number);
              cursor cur(p number) is select * from t where x = p;
              procedure fwd;
              function ext return number as language c name "x";
              function f(p_a number, p_b varchar2 default 'x') return number deterministic is
                c_l constant number := 2;
                procedure inner is
                  c_i constant number := 3;
                begin
                  null;
                end inner;
              begin
                if p_a > 0 then
                  for i in 1 .. 10 loop
                    case p_b when 'x' then null; else null; end case;
                  end loop;
                end if;
                return case when c_l > 0 then 1 else 0 end;
              end f;
              procedure fwd is
              begin
                begin
                  select c_g, c_l, c_i from dual§;
                end;
              end;
            begin
              null;
            end pkg;
            """;
        // c_l and c_i are out of scope here: they are not touched (no declaration visible).
        Assert.Equal("select 1 /*c_g*/, c_l, c_i from dual", Copy(text));
    }

    [Fact]
    public void UnterminatedTextDoesNotFail()
    {
        Assert.Equal("select 'abc", Copy("declare c constant number := 1; begin select 'abc§"));
        Assert.Equal("select \"x", Copy("select \"x§"));
        Assert.Equal("select q'[x", Copy("select q'[x§"));
        Assert.Equal("select 1 /*c*/ from dual", Copy("declare c constant number := 1; begin select c from dual§ /* open"));
    }

    [Fact]
    public void DeepChainsStopAtTheLimit()
    {
        var decls = string.Join(" ", Enumerable.Range(0, 15).Select(i => $"c{i + 1} constant number := c{i} + 1;"));
        var r = Run($"declare c0 constant number := 0; {decls} begin select c15, c3 from dual§; end;");
        Assert.Equal("select :c15, (((0 + 1) + 1) + 1) /*c3*/ from dual", r.Text);
        Assert.StartsWith("1 constant, 1 bind variable; constants as binds: c15", r.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ManyUnresolvedNamesAreShortened()
    {
        var r = Run("declare a constant boolean := true; b constant boolean := true; c constant boolean := true; d constant boolean := true;"
                    + " e constant boolean := true; f constant boolean := true; begin select a, b, c, d, e, f from dual§; end;");
        Assert.EndsWith("constants as binds: a, b, c, d, e, ...", r.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleConstantAndBindAreNamedInSingular() =>
        Assert.Equal("1 constant, 1 bind variable", Run("procedure p(x number) is c constant number := 1; begin delete t where a = c and b = x§; end;").Summary);

    [Fact]
    public void SpecQueryWithOwnerIsAnIndexedLookup() =>
        Assert.Equal("select text from all_source where owner = 'HR' and name = 'PKG' and type = 'PACKAGE' order by line", SpecQuery.Source("HR", "PKG"));

    [Fact]
    public void SpecQueryWithoutOwnerReadsUserSourceAndEscapesQuotes() =>
        Assert.Equal("select text from user_source where name = 'it''s' and type = 'PACKAGE' order by line", SpecQuery.Source(null, "it's"));

    [Fact]
    public void PackageVariablesWithAnInitialValueAreUsedLikeConstants()
    {
        var specs = new FakeSpecs(new(StringComparer.Ordinal)
        {
            ["ACCOUNT_DATA2"] = """
                create or replace package account_data2 is
                  c_active_state number(2) := 1;
                  c_not_found varchar2(2) := '-1';
                  g_tab t_tab := t_tab();
                  g_last number;
                end;
                """,
        });
        var r = Run("""
            create or replace package body account_data2 is
              g_local pls_integer default 7;
              procedure p is
                l_x number := 5;
              begin
                select * from account t where t.state = c_active_state and t.code = c_not_found and t.n = g_local and t.m = l_x and t.k = g_tab and t.z = g_last§;
              end;
            end;
            """, specs);
        Assert.Equal("select * from account t where t.state = 1 /*c_active_state*/ and t.code = '-1' /*c_not_found*/ and t.n = 7 /*g_local*/"
                     + " and t.m = :l_x and t.k = :g_tab and t.z = :g_last", r.Text);
    }

    [Fact]
    public void SynonymQueryPrefersAPrivateSynonym()
    {
        var sql = SpecQuery.Synonym("PKG");
        Assert.Contains("synonym_name = 'PKG'", sql, StringComparison.Ordinal);
        Assert.EndsWith("order by case owner when 'PUBLIC' then 2 else 1 end", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("HR.PKG", "HR", "PKG")]
    [InlineData(null, null, null)]
    [InlineData("", null, null)]
    [InlineData(".PKG", null, null)]
    [InlineData("HR.", null, null)]
    [InlineData("NODOT", null, null)]
    public void SynonymTargetIsParsedSafely(string? row, string? owner, string? name)
    {
        var t = SpecQuery.ParseTarget(row);
        Assert.Equal(owner, t?.Owner);
        Assert.Equal(name, t?.Name);
    }

    [Fact]
    public void LexerKeepsEveryTokenInsideTheText()
    {
        const string text = "select q'<a>' , Nq'(b)', N'c', \"q\", x$#, 1e, 1e+, -- c\n /* x */ :=, => ;";
        var tokens = PlsqlLexer.Tokenize(text);
        Assert.All(tokens, t => Assert.InRange(t.End, t.Start + 1, text.Length));
        Assert.Contains(tokens, t => t.Kind == TokenKind.StringLiteral && string.Equals(text[t.Start..t.End], "Nq'(b)'", StringComparison.Ordinal));
        Assert.Contains(tokens, t => t.Kind == TokenKind.QuotedName);
    }
}
