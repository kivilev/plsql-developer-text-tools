namespace TextTools.Plugin.Tests;

public class BatchFormatTests
{
    [Theory]
    [InlineData("a b c", new[] { "a", "b", "c" })]
    [InlineData("  a   b  ", new[] { "a", "b" })]
    [InlineData("\"C:/My Files/raw\" out result", new[] { "C:/My Files/raw", "out", "result" })]
    [InlineData("", new string[0])]
    [InlineData("\"\" x", new[] { "", "x" })]
    public void ArgumentsAreSplitAtBlanksAndQuotesKeepThemTogether(string text, string[] expected) =>
        Assert.Equal(expected, BatchFormat.SplitArguments(text));
}
