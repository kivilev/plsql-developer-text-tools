namespace TextTools.Core;

/// <summary>Splits text into lines and joins it back, preserving the line ending style and a trailing newline.</summary>
public readonly record struct Lines(IReadOnlyList<string> Items, string Eol, bool TrailingNewline)
{
    public static Lines Split(string text)
    {
        var eol = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var trailing = text.EndsWith('\n');
        var trailingLength = text.EndsWith("\r\n", StringComparison.Ordinal) ? 2 : 1;
        var body = trailing ? text[..^trailingLength] : text;
        var items = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return new Lines(items, eol, trailing);
    }

    public string Join() => string.Join(Eol, Items) + (TrailingNewline ? Eol : "");

    public static string Map(string text, Func<IReadOnlyList<string>, IEnumerable<string>> f)
    {
        var lines = Split(text);
        return (lines with { Items = f(lines.Items).ToList() }).Join();
    }
}
