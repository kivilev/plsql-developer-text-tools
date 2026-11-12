using TextTools.Core.Formatting;

namespace TextTools.Core.Tests;

/// <summary>
/// The rules of the ICBS standard after release 0.4.0 (coding-standards.md sections 5-6, cbs_post.py of the format review): each
/// rule on its own gives the layout of the reference formatter and is a fixed point; with every rule off the text is untouched.
/// </summary>
public class IcbsReviewRuleTests
{
    private static readonly FormatOptions Wrapping = FormatOptions.None with { WrapLongLines = true };

    private static readonly Dictionary<string, (FormatOptions On, string Input, string Expected)> Examples = new(StringComparer.Ordinal)
    {
        ["AssignmentSingleSpace"] = (FormatOptions.None with { AssignmentSingleSpace = true },
            "BEGIN\n  a         := 1;\n  long_name:=2;\nEND;\n",
            "BEGIN\n  a := 1;\n  long_name := 2;\nEND;\n"),
        ["TightUnaryMinus"] = (FormatOptions.None with { TightUnaryMinus = true },
            "BEGIN\n  IF v IN (- 4068, - 4061) THEN\n    x := - 1;\n  END IF;\nEND;\n",
            "BEGIN\n  IF v IN (-4068, -4061) THEN\n    x := - 1;\n  END IF;\nEND;\n"),
        ["OneSpaceBeforeComment"] = (FormatOptions.None with { OneSpaceBeforeComment = true },
            "BEGIN\n  x := 1;      -- one\n  -- alone\n  y := '--';   -- two\nEND;\n",
            "BEGIN\n  x := 1; -- one\n  -- alone\n  y := '--'; -- two\nEND;\n"),
        ["CaseLayoutShort"] = (FormatOptions.None with { CaseLayout = true },
            "BEGIN\n  SELECT CASE\n           WHEN a.state = 1 THEN\n            'A'\n           ELSE\n            'I'\n         END\n    INTO v\n    FROM t a;\nEND;\n",
            "BEGIN\n  SELECT CASE WHEN a.state = 1 THEN 'A' ELSE 'I' END\n    INTO v\n    FROM t a;\nEND;\n"),
        ["CaseLayoutLong"] = (FormatOptions.None with { CaseLayout = true },
            "BEGIN\n  SELECT CASE\n           WHEN a.state = 1 THEN\n            'Active account of the client'\n           WHEN a.state = 2 THEN\n            'Closed'\n           ELSE\n            'Unknown'\n         END\n    INTO v\n    FROM t a;\nEND;\n",
            "BEGIN\n  SELECT CASE\n           WHEN a.state = 1 THEN\n             'Active account of the client'\n           WHEN a.state = 2 THEN\n             'Closed'\n           ELSE\n             'Unknown'\n         END\n    INTO v\n    FROM t a;\nEND;\n"),
        ["TypeFieldsRecord"] = (FormatOptions.None with { TypeFieldsLayout = true },
            "DECLARE\n  TYPE t_rec IS RECORD(\n    id   NUMBER,\n    name VARCHAR2(80));\nBEGIN\n  NULL;\nEND;\n",
            "DECLARE\n  TYPE t_rec IS RECORD (\n      id   NUMBER,\n      name VARCHAR2(80)\n  );\nBEGIN\n  NULL;\nEND;\n"),
        ["TypeFieldsObject"] = (FormatOptions.None with { TypeFieldsLayout = true },
            "CREATE OR REPLACE TYPE t AS OBJECT\n(\n  id   NUMBER,\n  name VARCHAR2(80)\n)\n",
            "CREATE OR REPLACE TYPE t AS OBJECT (\n    id   NUMBER,\n    name VARCHAR2(80)\n)\n"),
        ["SelectListFill"] = (FormatOptions.None with { SelectListFill = true },
            "BEGIN\n  SELECT DISTINCT a.id,\n                  a.branch,\n                  a.name\n    INTO v_id,\n         v_branch,\n         v_name\n    FROM t a;\nEND;\n",
            "BEGIN\n  SELECT DISTINCT a.id, a.branch, a.name\n    INTO v_id, v_branch, v_name\n    FROM t a;\nEND;\n"),
        ["SelectListFillView"] = (FormatOptions.None with { SelectListFill = true },
            "CREATE OR REPLACE VIEW v (\n  id,\n  branch,\n  name\n) AS\nSELECT id, branch, name FROM t;\n",
            "CREATE OR REPLACE VIEW v (\n  id, branch, name\n) AS\nSELECT id, branch, name FROM t;\n"),
        ["TableAliasSingleSpace"] = (FormatOptions.None with { TableAliasSingleSpace = true },
            "BEGIN\n  SELECT a.id\n    INTO v\n    FROM account     a\n    JOIN client_data c\n      ON c.id = a.client;\nEND;\n",
            "BEGIN\n  SELECT a.id\n    INTO v\n    FROM account a\n    JOIN client_data c\n      ON c.id = a.client;\nEND;\n"),
        ["BulkCollectOnOneLine"] = (FormatOptions.None with { BulkCollectOnOneLine = true },
            "BEGIN\n  SELECT a.id\n    BULK COLLECT\n    INTO v_ids\n    FROM t a;\nEND;\n",
            "BEGIN\n  SELECT a.id\n    BULK COLLECT INTO v_ids\n    FROM t a;\nEND;\n"),
        ["JsonArrayaggLayout"] = (FormatOptions.None with { JsonArrayaggLayout = true },
            "BEGIN\n  SELECT JSON_ARRAYAGG(JSON_OBJECT(\n                         'id' VALUE a.id,\n                         'data' VALUE a.data\n                         FORMAT JSON\n                       ))\n    INTO v\n    FROM t a;\nEND;\n",
            "BEGIN\n  SELECT JSON_ARRAYAGG(\n           JSON_OBJECT(\n             'id' VALUE a.id,\n             'data' VALUE a.data FORMAT JSON\n           ))\n    INTO v\n    FROM t a;\nEND;\n"),
        ["JsonTableLayout"] = (FormatOptions.None with { JsonTableLayout = true },
            "BEGIN\n  SELECT jt.code\n    INTO v\n    FROM JSON_TABLE(v_json,\n                    '$'\n                    COLUMNS(code NUMBER PATH '$.code', name VARCHAR2(10) PATH '$.name')) jt;\nEND;\n",
            "BEGIN\n  SELECT jt.code\n    INTO v\n    FROM JSON_TABLE(v_json, '$'\n                    COLUMNS (code NUMBER PATH '$.code',\n                             name VARCHAR2(10) PATH '$.name')) jt;\nEND;\n"),
        ["ConditionPerLine"] = (Wrapping with { ConditionPerLine = true },
            "BEGIN\n  IF v_account.state = account_const.c_state_active AND v_account.currency = i_currency THEN\n    NULL;\n  END IF;\nEND;\n",
            "BEGIN\n  IF v_account.state = account_const.c_state_active\n     AND v_account.currency = i_currency\n  THEN\n    NULL;\n  END IF;\nEND;\n"),
        ["ConditionPerLineReturn"] = (FormatOptions.None with { ConditionPerLine = true },
            "BEGIN\n  RETURN v_a = 1\n      OR v_b = 2;\nEND;\n",
            "BEGIN\n  RETURN v_a = 1 OR v_b = 2;\nEND;\n"),
        ["FillLiteralArguments"] = (Wrapping with { FillLiteralArguments = true },
            "BEGIN\n  pkg.init(NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);\nEND;\n",
            "BEGIN\n  pkg.init(NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,\n           NULL);\nEND;\n"),
        ["WrapInLists"] = (Wrapping with { WrapInLists = true },
            "BEGIN\n  IF v IN (-4068, -4061, -4065, -6508, -6512, -20001, -20002, -20003, -20004) THEN\n    NULL;\n  END IF;\nEND;\n",
            "BEGIN\n  IF v IN (-4068,\n           -4061,\n           -4065,\n           -6508,\n           -6512,\n           -20001,\n           -20002,\n           -20003,\n           -20004) THEN\n    NULL;\n  END IF;\nEND;\n"),
        ["KeepShortArguments"] = (Wrapping with { KeepShortArguments = true },
            "BEGIN\n  v := deposit_checks.is_valid_new_book_dep_params(i_branch, i_filial, i_code);\n  w := SUBSTR('a long message text that does not fit in the margin at all' || SQLERRM, 1, 2000);\nEND;\n",
            "BEGIN\n  v := deposit_checks.is_valid_new_book_dep_params(i_branch, i_filial, i_code);\n  w := SUBSTR('a long message text that does not fit in the margin at all' || SQLERRM,\n              1, 2000);\nEND;\n"),
        ["WrapArithmetic"] = (Wrapping with { WrapArithmetic = true },
            "BEGIN\n  v := account_data.get_saldo(i_branch, 3) + account_data.get_saldo(i_branch, 8) + v_rest;\nEND;\n",
            "BEGIN\n  v := account_data.get_saldo(i_branch, 3) +\n       account_data.get_saldo(i_branch, 8) +\n       v_rest;\nEND;\n"),
        ["NoBlankAtBlockEdges"] = (FormatOptions.None with { NoBlankAtBlockEdges = true },
            "BEGIN\n\n  IF a = 1 THEN\n\n    x := 1;\n\n    y := 2;\n\n  END IF;\n\nEND;\n",
            "BEGIN\n  IF a = 1 THEN\n    x := 1;\n\n    y := 2;\n  END IF;\n\nEND;\n"),
        ["LowerCapitalNames"] = (FormatOptions.None with { LowerCapitalNames = true },
            "BEGIN\n  SELECT A.ID, oJClient.shortName, \"QUOTED\"\n    INTO V_ID, v_x, v_y\n    FROM ACCOUNT A\n   WHERE A.ID = :NEW.ID AND ROWID = 1 AND V_TAB.COUNT > 0;\n  DBMS_ASSERT.SIMPLE_SQL_NAME(V_NAME);\nEND;\n",
            "BEGIN\n  SELECT a.id, oJClient.shortName, \"QUOTED\"\n    INTO v_id, v_x, v_y\n    FROM account a\n   WHERE a.id = :NEW.id AND ROWID = 1 AND v_tab.COUNT > 0;\n  DBMS_ASSERT.SIMPLE_SQL_NAME(v_name);\nEND;\n"),
        ["UpperContextKeywords"] = (FormatOptions.None with { UpperContextKeywords = true },
            "BEGIN\n  SELECT jt.x\n    INTO v\n    FROM t a\n    CROSS apply json_table(a.j, '$' columns(x NUMBER path '$.x')) jt\n   WHERE rownum = 1;\n  utl_http.end_of_body;\nEXCEPTION\n  WHEN no_data_found THEN\n    NULL;\nEND;\n",
            "BEGIN\n  SELECT jt.x\n    INTO v\n    FROM t a\n    CROSS APPLY json_table(a.j, '$' COLUMNS(x NUMBER PATH '$.x')) jt\n   WHERE ROWNUM = 1;\n  UTL_HTTP.end_of_body;\nEXCEPTION\n  WHEN NO_DATA_FOUND THEN\n    NULL;\nEND;\n"),
    };

    public static TheoryData<string> Names() => [.. Examples.Keys];

    [Theory]
    [MemberData(nameof(Names))]
    public void TheRuleOnItsOwnGivesTheReferenceLayout(string name)
    {
        var (on, input, expected) = Examples[name];
        Assert.True(PostFormatter.TryFormat(input, on, out var result), "the token guard objected");
        Assert.Equal(expected, result);
        Assert.Equal(expected, PostFormatter.Format(expected, on));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void SwitchedOffTheTextIsUntouched(string name)
    {
        var (_, input, _) = Examples[name];
        Assert.Equal(input, PostFormatter.Format(input, FormatOptions.None));
    }

    [Fact]
    public void ASelectListItemIsNotTakenForAStatementWhenCallsAreJoined()
    {
        const string text = "BEGIN\n  SELECT c.branch,\n         SUBSTR(c.def_atm_account, -20)\n    INTO o_branch,\n         v_account\n    FROM client c\n   WHERE c.id = i_client;\nEND;\n";
        var result = PostFormatter.Format(text);
        Assert.Contains("\n    INTO o_branch, v_account\n    FROM client c\n", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AMultiLineCallStatementOfShortArgumentsIsJoinedWithin120()
    {
        const string text = "BEGIN\n  RETURN client_jur_t(NULL,\n                      NULL,\n                      NULL);\nEND;\n";
        Assert.Equal("BEGIN\n  RETURN client_jur_t(NULL, NULL, NULL);\nEND;\n", PostFormatter.Format(text));
        Assert.Equal(text, PostFormatter.Format(text, FormatOptions.Default with { KeepShortArguments = false }));
    }

    [Fact]
    public void ACallInASelectListKeepsTheMarginWhenWholeStatementsAreJoined()
    {
        const string text = "BEGIN\n  SELECT\n    some_function(\n      very_long_first_column_name,\n      very_long_second_column_name) FROM source_table;\nEND;\n";
        var result = PostFormatter.Format(text, FormatOptions.None with { WrapLongLines = true, KeepShortArguments = true });
        Assert.All(result.Split('\n'), l => Assert.True(l.Length <= 76, l));
    }

    [Fact]
    public void TheThenOfACaseInsideAConditionIsNotTheThenOfTheIf()
    {
        const string text = "BEGIN\n  IF x = CASE\n           WHEN y = 1 THEN\n             2\n           ELSE\n             3\n         END\n     AND z = 1 THEN\n    NULL;\n  END IF;\nEND;\n";
        var result = PostFormatter.Format(text, FormatOptions.None with { ConditionPerLine = true });
        Assert.Contains("           WHEN y = 1 THEN\n", result, StringComparison.Ordinal);
        Assert.Contains("     AND z = 1\n  THEN\n", result, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonArrayaggLayoutOfALongTextWithoutJsonIsFast()
    {
        var text = "BEGIN\n" + string.Concat(Enumerable.Repeat("  NULL;\n", 50_000)) + "END;";
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Equal(text, PostFormatter.Format(text, FormatOptions.None with { JsonArrayaggLayout = true }));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
    }

    [Fact]
    public void QuotedAndMixedCaseNamesKeepTheirCase()
    {
        const string text = "BEGIN\n  SELECT \"ID\", oJClient.shortName INTO v FROM t;\nEND;\n";
        Assert.Equal(text, PostFormatter.Format(text, FormatOptions.None with { LowerCapitalNames = true }));
    }
}
