using TextTools.Core.Formatting;
using TextTools.Core.Sql;

namespace TextTools.Core.Tests;

public class PostFormatterTests
{
    private const string Beautified = """
        CREATE OR REPLACE PACKAGE BODY account_api IS
          PROCEDURE open_account(i_id IN NUMBER) IS
            v_acc account%rowtype;
          BEGIN
            v_x := nvl(i_id, 0);
            v_longer_name := 1;
            EXECUTE IMMEDIATE 'update t set a = 1 where b = 2 and c = 3 and d = 4 and e = 5 and f = 6';
          END;
          FUNCTION is_ok RETURN BOOLEAN IS
          BEGIN
            RETURN TRUE;
          END;

        END;
        """;

    private const string Expected = """
        CREATE OR REPLACE PACKAGE BODY account_api IS
          PROCEDURE open_account(i_id IN NUMBER) IS
            v_acc account%ROWTYPE;
          BEGIN
            v_x           := NVL(i_id, 0);
            v_longer_name := 1;
            EXECUTE IMMEDIATE
              'update t set a = 1 where b = 2 and c = 3 and d = 4 and e = 5 and f = 6';
          END open_account;

          FUNCTION is_ok RETURN BOOLEAN IS
          BEGIN
            RETURN TRUE;
          END is_ok;
        END account_api;
        """;

    [Fact]
    public void AppliesTheRulesToABeautifiedPackageBody() => Assert.Equal(Expected, PostFormatter.Format(Beautified));

    [Fact]
    public void IsIdempotent()
    {
        var once = PostFormatter.Format(Beautified);
        Assert.Equal(once, PostFormatter.Format(once));
    }

    [Fact]
    public void KeepsCrlfWhenEveryLineEndIsCrlf()
    {
        var crlf = Beautified.Replace("\n", "\r\n", StringComparison.Ordinal);
        Assert.Equal(Expected.Replace("\n", "\r\n", StringComparison.Ordinal), PostFormatter.Format(crlf));
    }

    [Fact]
    public void MixedLineEndsBecomeLf()
    {
        var mixed = "BEGIN\r\n  x:=1;\nEND;";
        Assert.DoesNotContain('\r', PostFormatter.Format(mixed));
    }

    [Fact]
    public void LeavesCommentsAndLiteralsAlone()
    {
        const string sql = "BEGIN\n  -- nvl(x, 1) and %type stay\n  v := 'nvl(1) %rowtype';\n  /* trunc(a) */\nEND;";
        Assert.Equal("BEGIN\n  -- nvl(x, 1) and %type stay\n  v := 'nvl(1) %rowtype';\n  /* trunc(a) */\nEND;", PostFormatter.Format(sql));
    }

    [Theory]
    [InlineData("x := nvl(a, b);", "x := NVL(a, b);")]
    [InlineData("x := pkg.nvl(a, b);", "x := pkg.nvl(a, b);")] // a package's own function
    [InlineData("x := my_func(a);", "x := my_func(a);")]
    [InlineData("x := SubStr (a, 1);", "x := SUBSTR (a, 1);")]
    [InlineData("v t.c%type;", "v t.c%TYPE;")]
    [InlineData("IF c%notfound THEN", "IF c%NOTFOUND THEN")]
    public void CaseRules(string input, string expected) =>
        Assert.Equal(expected, CaseRulesApply(input));

    private static string CaseRulesApply(string text) =>
        PostFormatter.Format(text, new FormatOptions(EndWithName: false, AlignAssignments: false, BlankLineBeforeSubprograms: false,
            NoBlankLineBeforePackageEnd: false, ExecuteImmediateLiteralOnOwnLine: false));

    [Fact]
    public void AlignsAssignmentGroupsOfTheSameIndentOnly()
    {
        const string input = "BEGIN\n  a := 1;\n  long_name := 2;\n  IF x THEN\n    b := 3;\n    cc := 4;\n  END IF;\nEND;";
        const string expected = "BEGIN\n  a         := 1;\n  long_name := 2;\n  IF x THEN\n    b  := 3;\n    cc := 4;\n  END IF;\nEND;";
        Assert.Equal(expected, PostFormatter.Format(input));
    }

    [Fact]
    public void AMultiLineAssignmentEndsItsGroup()
    {
        const string input = "BEGIN\n  a := f(1,\n         2);\n  bb := 3;\nEND;";
        var result = PostFormatter.Format(input);
        Assert.Contains("  a := f(1,", result, StringComparison.Ordinal);
        Assert.Contains("  bb := 3;", result, StringComparison.Ordinal); // a group of one: single spaces
    }

    [Fact]
    public void EndGetsTheNameOfItsSubprogramOnly()
    {
        const string input = "CREATE OR REPLACE PACKAGE p IS\n  PROCEDURE a;\nEND;\n/\nCREATE OR REPLACE PROCEDURE q IS\nBEGIN\n  IF x THEN\n    NULL;\n  END IF;\nEND;";
        var result = PostFormatter.Format(input);
        Assert.Contains("\nEND p;\n", result, StringComparison.Ordinal);
        Assert.EndsWith("\nEND q;", result, StringComparison.Ordinal);
        Assert.Contains("END IF;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ABlockEndIsNotNamedAndASchemaPrefixIsDropped()
    {
        const string input = "CREATE OR REPLACE PROCEDURE icbs.q IS\nBEGIN\n  BEGIN\n    NULL;\n  EXCEPTION WHEN OTHERS THEN NULL;\n  END;\nEND;";
        var result = PostFormatter.Format(input);
        Assert.Contains("\n  END;\n", result, StringComparison.Ordinal); // the inner block: other indent
        Assert.EndsWith("\nEND q;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BlankLineBeforeASubprogramAndItsCommentButNotAfterIs()
    {
        const string input = "PACKAGE p IS\n  PROCEDURE a;\n  -- about b\n  PROCEDURE b;\nEND;";
        Assert.Equal("PACKAGE p IS\n  PROCEDURE a;\n\n  -- about b\n  PROCEDURE b;\nEND p;", PostFormatter.Format(input));
    }

    [Fact]
    public void NoBlankLineBeforeTheClosingEndOfAPackage()
    {
        Assert.Equal("PACKAGE BODY p IS\n  x NUMBER;\nEND p;", PostFormatter.Format("PACKAGE BODY p IS\n  x NUMBER;\n\n\nEND;"));
    }

    [Fact]
    public void ShortExecuteImmediateStaysOnOneLine() =>
        Assert.Equal("BEGIN\n  EXECUTE IMMEDIATE 'x';\nEND;", PostFormatter.Format("BEGIN\n  EXECUTE IMMEDIATE 'x';\nEND;"));

    [Fact]
    public void MultiLineLiteralOfExecuteImmediateStartsOnItsOwnLine()
    {
        const string input = "BEGIN\n  EXECUTE IMMEDIATE 'select 1\n  from dual' INTO v;\nEND;";
        Assert.Equal("BEGIN\n  EXECUTE IMMEDIATE\n    'select 1\n  from dual' INTO v;\nEND;", PostFormatter.Format(input));
    }

    [Fact]
    public void ExecuteImmediateInACommentIsNotTouched()
    {
        const string input = "BEGIN\n  /*\n  EXECUTE IMMEDIATE 'a very long text that is longer than the margin of seventy six characters for sure'\n  */\n  NULL;\nEND;";
        Assert.Equal(input, PostFormatter.Format(input));
    }

    [Fact]
    public void TrailingBlanksGoExceptInsideAMultiLineLiteral()
    {
        Assert.Equal("BEGIN  \n  v := 'a  \nb';\nEND;".Replace("BEGIN  ", "BEGIN", StringComparison.Ordinal), PostFormatter.Format("BEGIN  \n  v := 'a  \nb';  \nEND;"));
    }

    [Fact]
    public void EveryRuleCanBeSwitchedOff()
    {
        var off = new FormatOptions(BuiltinFunctionsUpper: false, AttributesUpper: false, EndWithName: false, AlignAssignments: false,
            BlankLineBeforeSubprograms: false, NoBlankLineBeforePackageEnd: false, ExecuteImmediateLiteralOnOwnLine: false, TrimTrailingSpaces: false);
        Assert.Equal(Beautified, PostFormatter.Format(Beautified, off));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n")]
    public void BlankInputIsReturnedAsIs(string text) => Assert.Equal(text, PostFormatter.Format(text));

    [Fact]
    public void AGuardedFailureReturnsTheInputUntouched()
    {
        // A rule that changed a token would be caught: simulate by comparing different texts.
        Assert.False(TokenGuard.Same("a := 1;", "a := 2;"));
        Assert.False(TokenGuard.Same("-- one", "-- two"));
        Assert.False(TokenGuard.Same("x := 'a';", "x := 'A';"));
        Assert.True(TokenGuard.Same("x := NVL(a);", "x   :=   nvl(a) ;"));
        Assert.True(TokenGuard.Same("END;", "END name;"));
        Assert.False(TokenGuard.Same("END IF;", "END LOOP;"));
        Assert.True(TokenGuard.Same("a  -- c  \n", "a  -- c\n"));
    }

    [Fact]
    public void NeverThrowsAndNeverChangesTokensOnRandomInput()
    {
        var rnd = new Random(11);
        string[] pieces = ["BEGIN", "END;", "END", "PROCEDURE p IS", "FUNCTION f RETURN NUMBER IS", "x := 1;", "'q", "q'[", "-- c", "/*", "*/", "nvl(", "(", ")", ";", "\n", "\r\n", "  ", "%type", "EXECUTE IMMEDIATE '", "\"A\"", "\t"];
        for (var n = 0; n < 400; n++)
        {
            var text = string.Concat(Enumerable.Range(0, rnd.Next(1, 40)).Select(_ => pieces[rnd.Next(pieces.Length)] + (rnd.Next(3) == 0 ? " " : "")));
            var once = PostFormatter.Format(text);
            Assert.True(TokenGuard.Same(text.Replace("\r\n", "\n", StringComparison.Ordinal), once.Replace("\r\n", "\n", StringComparison.Ordinal)), text);
            Assert.Equal(PlsqlLexer.Mask(once).Length, once.Length);
        }
    }

    [Fact]
    public void MaskBlanksCommentsAndFillsLiteralsKeepingLengthAndLines()
    {
        const string text = "a := 'it''s'; -- note\nb := q'[x]'; /* c\nd */ \"Q\" N'z'";
        var mask = PlsqlLexer.Mask(text);
        Assert.Equal(text.Length, mask.Length);
        Assert.Equal(text.Count(c => c == '\n'), mask.Count(c => c == '\n'));
        Assert.Equal("a := xxxxxxx;        \nb := xxxxxx;     \n     xxx xxxx", mask);
    }
}
