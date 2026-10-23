using TextTools.Core.Formatting;

namespace TextTools.Core.Tests;

public class SyntaxHighlighterTests
{
    /// <summary>K keyword, L literal, C comment, . everything else (blanks inside a comment are not marked).</summary>
    private static string Marks(string text) =>
        string.Concat(SyntaxHighlighter.Classify(text).Select(c => c switch { CodeClass.Keyword => 'K', CodeClass.Literal => 'L', CodeClass.Comment => 'C', _ => '.' }));

    [Fact]
    public void ClassifiesKeywordsAndNames() => Assert.Equal("KKKKKK...KKKK..", Marks("select a from t"));

    [Theory]
    [InlineData("begin", "KKKKK")]
    [InlineData("BeGiN", "KKKKK")]
    [InlineData("v_name", "......")]
    [InlineData("begin_x", ".......")]
    public void KeywordsAreWholeWordsWithoutRegardToCase(string text, string expected) => Assert.Equal(expected, Marks(text));

    [Theory]
    [InlineData("'it''s'", "LLLLLLL")]
    [InlineData("\"Name\" 3.14", "LLLLLL.LLLL")]
    [InlineData("q'[x]'", "LLLLLL")]
    public void StringsQuotedNamesAndNumbersAreLiterals(string text, string expected) => Assert.Equal(expected, Marks(text));

    [Theory]
    [InlineData("x -- ab", "..CC.CC")]
    [InlineData("/* ab */ if", "CC.CC.CC.KK")]
    public void CommentsOfBothKindsAreComments(string text, string expected) => Assert.Equal(expected, Marks(text));

    [Fact]
    public void KeywordsInsideLiteralsAndCommentsAreNotKeywords()
    {
        Assert.DoesNotContain('K', Marks("'select from' -- begin end"));
        Assert.DoesNotContain('K', Marks("/* if then */"));
    }

    [Fact]
    public void NeverThrowsAndKeepsLength()
    {
        foreach (var text in new[] { "", "'", "q'[", "/*", "\"", "--", "\n\n", "end;\r\nbegin" })
        {
            Assert.Equal(text.Length, SyntaxHighlighter.Classify(text).Length);
        }
    }
}
