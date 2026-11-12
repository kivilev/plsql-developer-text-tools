namespace TextTools.Core.Formatting;

/// <summary>Exactly one blank before a <c>--</c> comment at the end of a code line: the comments are not aligned in a column.</summary>
internal static class CommentSpaceRule
{
    public static string Apply(string text)
    {
        var (lines, masked) = LineText.Split(text);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var m = masked[i];
            var p = -1;
            for (var k = 0; k + 1 < line.Length; k++)
            {
                if (line[k] == '-' && line[k + 1] == '-' && m[k] == ' ')
                {
                    p = k;
                    break;
                }
            }
            if (p > 0 && m[..p].Trim().Length > 0 && line[p - 1] is ' ' or '\t') lines[i] = line[..p].TrimEnd() + " " + line[p..];
        }
        return string.Join('\n', lines);
    }
}
