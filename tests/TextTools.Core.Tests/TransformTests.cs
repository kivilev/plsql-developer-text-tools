namespace TextTools.Core.Tests;

public class CaseTests
{
    [Theory]
    [InlineData("first_name, last_name", "firstName, lastName")]
    [InlineData("XMLHttpRequest", "xmlHttpRequest")]
    [InlineData("user-account-id", "userAccountId")]
    [InlineData("l_SUM$total", "lSum$total")]
    public void Camel(string input, string expected) => Assert.Equal(expected, CaseConverter.ToCamel(input));

    [Theory]
    [InlineData("firstName lastName", "first_name last_name")]
    [InlineData("HTTPServerError2", "http_server_error2")]
    [InlineData("ClientID", "client_id")]
    public void Snake(string input, string expected) => Assert.Equal(expected, CaseConverter.ToSnake(input));

    [Fact]
    public void UpperSnakeKeepsPunctuation() =>
        Assert.Equal("SELECT FIRST_NAME FROM T_USER;", CaseConverter.ToUpperSnake("select firstName from tUser;"));

    [Fact]
    public void Pascal() => Assert.Equal("OrderItems", CaseConverter.ToPascal("order_items"));

    [Fact]
    public void Kebab() => Assert.Equal("order-items", CaseConverter.ToKebab("OrderItems"));

    [Fact]
    public void Cyrillic() => Assert.Equal("имяКлиента", CaseConverter.ToCamel("имя_клиента"));
}

public class SqlTextTests
{
    [Fact]
    public void EscapeAndUnescape()
    {
        Assert.Equal("it''s", SqlText.Escape("it's"));
        Assert.Equal("it's", SqlText.Unescape("'it''s'"));
        Assert.Equal("it's", SqlText.Unescape("it''s"));
        Assert.Equal("it's ok", SqlText.Unescape("q'[it's ok]'"));
    }

    [Fact]
    public void QQuotePicksFreeDelimiter()
    {
        Assert.Equal("q'[it's]'", SqlText.QQuote("it's"));
        Assert.Equal("q'{a]'b}'", SqlText.QQuote("a]'b"));
    }

    [Fact]
    public void InListQuotesStrings() =>
        Assert.Equal("IN ('a', 'O''Neil')", SqlText.ToInList("a\r\n  O'Neil \r\n\r\n"));

    [Fact]
    public void InListKeepsNumbers() => Assert.Equal("IN (1, 2.5, -3)", SqlText.ToInList("1\n2.5\n-3,"));

    // Issue #17: a single line of values is split too.
    [Theory]
    [InlineData("1 2 23  232\t", "IN (1, 2, 23, 232)")]
    [InlineData("1, 2,3;", "IN (1, 2, 3)")]
    [InlineData("a\tb\tO'Neil", "IN ('a', 'b', 'O''Neil')")]
    [InlineData("'a', 'it''s'", "IN ('a', 'it''s')")]
    [InlineData("'1', '2'", "IN ('1', '2')")]
    [InlineData("New York, Boston", "IN ('New York', 'Boston')")]
    public void InListSplitsASingleLine(string input, string expected) => Assert.Equal(expected, SqlText.ToInList(input));

    [Fact]
    public void InListKeepsOneValuePerLine() =>
        Assert.Equal("IN ('New York', 'Boston')", SqlText.ToInList("New York,\r\nBoston"));

    [Fact]
    public void InListOverLimitUsesCollection()
    {
        var input = string.Join("\n", Enumerable.Range(1, 1001));
        Assert.StartsWith("IN (SELECT column_value FROM TABLE(sys.odcinumberlist(1, 2,", SqlText.ToInList(input), StringComparison.Ordinal);
    }

    [Fact]
    public void Concatenation() =>
        Assert.Equal($"'a' || chr(10) ||{Environment.NewLine}'it''s'", SqlText.ToConcatenation("a\nit's"));
}

public class EncodeTests
{
    [Fact]
    public void Base64RoundTrip() => Assert.Equal("Привет", Encode.FromBase64(Encode.ToBase64("Привет")));

    [Fact]
    public void HexMatchesUtlRaw() => Assert.Equal("D094D0B0", Encode.ToHex("Да"));

    [Fact]
    public void HexDecodeAcceptsPrefixAndSpaces() => Assert.Equal("Да", Encode.FromHex("0xD094 D0B0"));

    [Fact]
    public void Url() => Assert.Equal("a b&c", Encode.FromUrl(Encode.ToUrl("a b&c")));
}

public class LineOpsTests
{
    [Fact]
    public void SortKeepsEolAndTrailingNewline() =>
        Assert.Equal("a\r\nb\r\nc\r\n", LineOps.SortAsc("c\r\na\r\nb\r\n"));

    [Fact]
    public void RemoveDuplicates() => Assert.Equal("a\nb", LineOps.RemoveDuplicates("a\nb\na"));

    [Fact]
    public void AlignByAssignment() => Assert.Equal(
        "  l_id   := 1;\n  -- note\n  l_name := 'x';",
        LineOps.AlignBy("  l_id := 1;\n  -- note\n  l_name:='x';", ":="));
}

public class SelectionLocatorTests
{
    private const string Text = "select a from t;\r\nselect a from u;";

    [Fact]
    public void SelectionEndingAtCursor()
    {
        // "a" on line 2 selected left-to-right, cursor after it: x = 9
        var r = SelectionLocator.Replace(Text, "a", 9, 2, s => s.ToUpperInvariant());
        Assert.Equal("select a from t;\r\nselect A from u;", r!.Value.Text);
        Assert.Equal((9, 2), (r.Value.CursorX, r.Value.CursorY));
    }

    [Fact]
    public void SelectionStartingAtCursor()
    {
        var r = SelectionLocator.Replace(Text, "a", 8, 1, s => "b");
        Assert.Equal("select b from t;\r\nselect a from u;", r!.Value.Text);
    }

    [Fact]
    public void AmbiguousSelectionWithBadCursorFails() =>
        Assert.Null(SelectionLocator.Replace(Text, "a", 1, 1, s => s));

    [Fact]
    public void UniqueSelectionFoundWithoutCursor() =>
        Assert.Equal(Text.Replace("u;", "U;", StringComparison.Ordinal), SelectionLocator.Replace(Text, "u;", 0, 0, s => s.ToUpperInvariant())!.Value.Text);
}

public class MenuLimitsTests
{
    [Fact]
    public void FitsIntoNinetyNineMenuIndices() =>
        // GROUP + one MENUITEM per menu + separators between groups inside each menu + commands
        Assert.True(1 + Commands.All.GroupBy(c => c.Menu).Sum(m => 1 + m.Select(c => c.Group).Distinct().Count() - 1)
                      + Commands.All.Count <= 99);

    // The IDE may read menu captions before OnActivate switches the interface to UTF-8.
    [Fact]
    public void CaptionsAreAscii() =>
        Assert.All(Commands.All, c => Assert.True((c.Group + c.Name).All(char.IsAscii), c.Name));

    // Status bar hint: single-line commands must have an example that fits the status bar.
    [Fact]
    public void DateAndShortStringCommandsShowAStatusBarExample() =>
        Assert.All(Commands.All.OfType<TransformCommand>().Where(c => string.Equals(c.Menu, Commands.DateMenu, StringComparison.Ordinal) || c.Group is "Case" or "Encode"),
            c => Assert.NotNull(c.StatusExample));

    [Fact]
    public void MultiLineExamplesStayOutOfTheStatusBar()
    {
        var sort = Commands.All.OfType<TransformCommand>().Single(c => string.Equals(c.Name, "Sort A-Z", StringComparison.Ordinal));
        Assert.Contains('\n', sort.Example!);
        Assert.Null(sort.StatusExample);
    }

    // The IDE treats "[...]" in a caption as options (like [tabindex=3]) and cuts it out.
    [Fact]
    public void CaptionsHaveNoSquareBrackets() =>
        Assert.All(Commands.All, c => Assert.DoesNotContain('[', c.Name));
}
