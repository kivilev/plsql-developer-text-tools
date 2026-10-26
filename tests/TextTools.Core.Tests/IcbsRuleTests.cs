using System.Text;
using TextTools.Core.Formatting;

namespace TextTools.Core.Tests;

/// <summary>
/// The layout rules of the ICBS standard (INSERT, SELECT ... INTO, DECODE, BETWEEN, UPDATE SET, subqueries, header tails, alias
/// padding, JOIN ... ON, ...): every file of <c>Cases/</c> is an input, a line "-----" and the output of the reference implementation.
/// The "gold" cases are the examples of the standard themselves: the canonical text must stay as it is.
/// </summary>
public class IcbsRuleTests
{
    public static TheoryData<string> Cases
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Cases"), "*.txt").Order(StringComparer.Ordinal)) data.Add(Path.GetFileNameWithoutExtension(file));
            return data;
        }
    }

    private static (string Input, string Expected) Load(string name)
    {
        var raw = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Cases", name + ".txt"), Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal);
        var cut = raw.IndexOf("\n-----\n", StringComparison.Ordinal);
        return (raw[..(cut + 1)], raw[(cut + 7)..]);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheCaseGivesTheExpectedTextAndIsStable(string name)
    {
        var (input, expected) = Load(name);
        Assert.Equal(expected, PostFormatter.Format(input));
        Assert.Equal(expected, PostFormatter.Format(expected)); // the result is a fixed point
    }

    [Theory]
    [InlineData("gold_sql")]
    [InlineData("gold_plsql")]
    [InlineData("gold_select_into")]
    public void TheGoldExamplesOfTheStandardStayAsTheyAre(string name)
    {
        var (input, expected) = Load(name);
        Assert.Equal(input, expected);
        Assert.Equal(input, PostFormatter.Format(input));
    }

    [Fact]
    public void AMultiLineStringLiteralStaysByteForByte()
    {
        const string text = "BEGIN\n  v_sql := 'SELECT a,   b\n  FROM t\n  SET\n    x:=1\n  WHERE  nvl(a,0)=1   \n  END;';\n  v_x:=nvl(1,2);\nEND;\n";
        var result = PostFormatter.Format(text);
        Assert.Contains("'SELECT a,   b\n  FROM t\n  SET\n    x:=1\n  WHERE  nvl(a,0)=1   \n  END;'", result, StringComparison.Ordinal);
        Assert.Contains("v_x := NVL(1, 2);", result, StringComparison.Ordinal); // the code around it is still formatted
    }

    [Fact]
    public void ARuleThatWouldChangeALiteralIsSkippedNotTheWholeFormatting()
    {
        // The reference would join the SET inside the literal with the next line; here that rule's result is dropped as a whole.
        const string text = "BEGIN\n  v_sql := 'a\n  SET\n    x = 1';\n  v_x:=1;\nEND;\n";
        var result = PostFormatter.Format(text);
        Assert.Contains("'a\n  SET\n    x = 1'", result, StringComparison.Ordinal);
        Assert.Contains("v_x := 1;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void EachRuleCanBeSwitchedOffAlone()
    {
        const string insert = "BEGIN\n  INSERT INTO t (a, b) VALUES (1, 2);\nEND;\n";
        Assert.Contains("VALUES\n", PostFormatter.Format(insert), StringComparison.Ordinal);
        Assert.Equal(insert, PostFormatter.Format(insert, FormatOptions.Default with { InsertLayout = false }));

        const string into = "BEGIN\n  SELECT a.x,\n         a.y\n    INTO v_x,\n         v_y\n    FROM t a;\nEND;\n";
        Assert.Contains("SELECT a.x, a.y\n", PostFormatter.Format(into), StringComparison.Ordinal);
        Assert.Equal(into, PostFormatter.Format(into, FormatOptions.Default with { SelectIntoOnOneLine = false }));

        const string decode = "BEGIN\n  v := DECODE(t.state, 0, 'Created', 1, 'Accepted for processing', 2, 'Rejected by the bank', 3, 'Paid', 'Unknown');\nEND;\n";
        Assert.Contains("0, 'Created',\n", PostFormatter.Format(decode), StringComparison.Ordinal);
        Assert.DoesNotContain("0, 'Created',\n", PostFormatter.Format(decode, FormatOptions.Default with { WrapDecodeInPairs = false }), StringComparison.Ordinal);

        const string call = "BEGIN\n  account_kernel.open_account(i_branch => v_branch, i_client => v_client, i_currency => v_currency, o_code => v_code);\nEND;\n";
        Assert.Contains("i_branch   =>", PostFormatter.Format(call), StringComparison.Ordinal);
        Assert.DoesNotContain("i_branch   =>", PostFormatter.Format(call, FormatOptions.Default with { AlignNamedArguments = false }), StringComparison.Ordinal);
        Assert.Equal(call, PostFormatter.Format(call, FormatOptions.Default with { WrapLongLines = false }));
    }

    [Fact]
    public void ALineEndingWithTheConcatenationOperatorDoesNotGetAnEmptyContinuation()
    {
        // The reference implementation turned the empty last piece into a blank line, and a second pass added another one.
        const string text = "BEGIN\n  v_message := 'Account ' || v_account || ' of the client ' || v_client || ' is closed on ' ||\n                 v_date;\nEND;\n";
        var once = PostFormatter.Format(text);
        Assert.DoesNotContain("\n\n", once, StringComparison.Ordinal);
        Assert.Equal(once, PostFormatter.Format(once));
    }

    [Fact]
    public void EveryOptionOffMeansTheTextStaysAsItIs()
    {
        var messy = "BEGIN\n  INSERT INTO t (a,b) VALUES (1,2);\n  v:=nvl( a,b );   \nEND;\n";
        Assert.Equal(messy, PostFormatter.Format(messy, FormatOptions.None));
    }

    [Fact]
    public void RandomPiecesNeverBreakTheTokensAndNeverThrow()
    {
        var rnd = new Random(23);
        string[] pieces =
        [
            "SELECT", "FROM", "INTO", "SET", "INSERT INTO t", "VALUES (", "(", ")", ",", "a,", "b", "JOIN x ON", "AND", "OR", "BETWEEN", "DECODE(", "x => 1,",
            "'q", "q'[", "-- c", "/*", "*/", ";", "\n", "\n", "  ", "IF", "THEN", "AUTHID CURRENT_USER", "IS", "FUNCTION f RETURN NUMBER", "||", ":=", "END;",
        ];
        for (var n = 0; n < 600; n++)
        {
            var text = string.Concat(Enumerable.Range(0, rnd.Next(1, 60)).Select(_ => pieces[rnd.Next(pieces.Length)] + (rnd.Next(2) == 0 ? " " : "")));
            Assert.True(PostFormatter.TryFormat(text, null, out var result), text);
            Assert.True(TokenGuard.Same(text, result), text);
        }
    }

    [Fact]
    public void ABlankAfterTheCommaIsOnByDefaultAndNeverTouchesLiteralsOrComments()
    {
        var text = "BEGIN\n  v := NVL(a,0) + f(x,y,'a,b'); -- c,d\n  g(1,\n    2);\nEND;\n";
        var on = FormatOptions.None with { SpaceAfterComma = true };
        Assert.Equal("BEGIN\n  v := NVL(a, 0) + f(x, y, 'a,b'); -- c,d\n  g(1,\n    2);\nEND;\n", PostFormatter.Format(text, on));
        Assert.Equal(text, PostFormatter.Format(text, FormatOptions.None));
        Assert.Contains("NVL(a, 0)", PostFormatter.Format(text), StringComparison.Ordinal); // on by default: the bank standard
    }

    [Fact]
    public void ABlankLineAfterThePackageHeaderWorksOnItsOwn()
    {
        var text = "CREATE OR REPLACE PACKAGE BODY p IS\n  PROCEDURE a IS\n  BEGIN\n    NULL;\n  END a;\n  PROCEDURE b IS\n  BEGIN\n    NULL;\n  END b;\nEND p;\n";
        var result = PostFormatter.Format(text, FormatOptions.None with { BlankLineAfterPackageHeader = true });
        Assert.StartsWith("CREATE OR REPLACE PACKAGE BODY p IS\n\n  PROCEDURE a IS\n", result, StringComparison.Ordinal);
        Assert.Contains("END a;\n  PROCEDURE b", result, StringComparison.Ordinal); // the other blank lines stay off
    }

    [Fact]
    public void SingleBlankBeforeAliasLeavesDeclarationsAndParametersAlone()
    {
        var text = "PROCEDURE p(i_a  IN  NUMBER,\n            o_b  OUT VARCHAR2) IS\nBEGIN\n  OPEN c FOR\n    SELECT a.id          account_id,\n           a.branch      branch_code\n      FROM account a;\nEND p;\n";
        var result = PostFormatter.Format(text, FormatOptions.None with { SingleBlankBeforeAlias = true });
        Assert.Contains("i_a  IN  NUMBER", result, StringComparison.Ordinal);
        Assert.Contains("a.id account_id,", result, StringComparison.Ordinal);
        Assert.Contains("a.branch branch_code", result, StringComparison.Ordinal);
    }
}
