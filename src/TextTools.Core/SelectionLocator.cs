namespace TextTools.Core;

public readonly record struct Replacement(string Text, int CursorX, int CursorY);

/// <summary>
/// PL/SQL Developer's IDE_SetText replaces the whole editor text, and the API does not expose selection bounds.
/// We know the full text, the selected text and the cursor (1-based X char, Y line), and the cursor sits at
/// one end of the selection — so the selection either ends or starts at the cursor offset.
/// </summary>
public static class SelectionLocator
{
    public static Replacement? Replace(string text, string selected, int cursorX, int cursorY, Func<string, string> transform)
    {
        var start = Locate(text, selected, cursorX, cursorY);
        if (start < 0) return null;

        var replaced = transform(selected);
        var newText = string.Concat(text.AsSpan(0, start), replaced, text.AsSpan(start + selected.Length));
        var (x, y) = ToPosition(newText, start + replaced.Length);
        return new Replacement(newText, x, y);
    }

    /// <summary>Inserts <paramref name="value"/> at the cursor; returns null if the cursor is outside the text.</summary>
    public static Replacement? Insert(string text, int cursorX, int cursorY, string value)
    {
        var offset = text.Length == 0 && cursorX <= 1 && cursorY <= 1 ? 0 : ToOffset(text, cursorX, cursorY);
        if (offset < 0) return null;
        var newText = text.Insert(offset, value);
        var (x, y) = ToPosition(newText, offset + value.Length);
        return new Replacement(newText, x, y);
    }

    /// <summary>Start offset of the selection in <paramref name="text"/>, or -1 if it cannot be determined.</summary>
    public static int Locate(string text, string selected, int cursorX, int cursorY)
    {
        if (selected.Length == 0) return -1;
        var cursor = ToOffset(text, cursorX, cursorY);
        if (cursor >= 0)
        {
            if (Matches(text, selected, cursor - selected.Length)) return cursor - selected.Length;
            if (Matches(text, selected, cursor)) return cursor;
        }
        // Fallback: the selected fragment is unique in the text.
        var first = text.IndexOf(selected, StringComparison.Ordinal);
        return first >= 0 && first == text.LastIndexOf(selected, StringComparison.Ordinal) ? first : -1;
    }

    /// <summary>1-based (x, y) → 0-based offset; lines are split on '\n' (a '\r' stays at the end of its line).</summary>
    public static int ToOffset(string text, int x, int y)
    {
        if (x < 1 || y < 1) return -1;
        var offset = 0;
        for (var line = 1; line < y; line++)
        {
            var nl = text.IndexOf('\n', offset);
            if (nl < 0) return -1;
            offset = nl + 1;
        }
        var lineEnd = text.IndexOf('\n', offset);
        if (lineEnd < 0) lineEnd = text.Length;
        if (lineEnd > offset && text[lineEnd - 1] == '\r') lineEnd--;
        return Math.Min(offset + x - 1, lineEnd);
    }

    public static (int X, int Y) ToPosition(string text, int offset)
    {
        var y = 1;
        var lineStart = 0;
        for (var i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                y++;
                lineStart = i + 1;
            }
        }
        return (offset - lineStart + 1, y);
    }

    private static bool Matches(string text, string selected, int start) =>
        start >= 0 && start + selected.Length <= text.Length &&
        string.CompareOrdinal(text, start, selected, 0, selected.Length) == 0;
}
