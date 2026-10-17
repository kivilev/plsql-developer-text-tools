using System.Runtime.InteropServices;

namespace TextTools.Core.Sql;

public enum TokenKind
{
    Word,
    QuotedName,
    StringLiteral,
    Number,
    Symbol,
}

/// <summary>A significant token: whitespace and comments are not tokens, the text between tokens is kept as is.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Token(TokenKind Kind, int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>
/// Splits SQL and PL/SQL into tokens. Never fails: an unterminated string, quoted name or comment runs to the end
/// of the text. Strings: 'a''b', N'..', q'[..]' (and nq'..') with any delimiter.
/// </summary>
public static class PlsqlLexer
{
    private static readonly string[] TwoCharSymbols = [":=", "=>", "..", "||", "<<", ">>", "!=", "<>", "<=", ">=", "**"];

    public static IReadOnlyList<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '-' && At(text, i + 1) == '-')
            {
                var nl = text.IndexOf('\n', i);
                i = nl < 0 ? text.Length : nl + 1;
            }
            else if (c == '/' && At(text, i + 1) == '*')
            {
                var close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? text.Length : close + 2;
            }
            else if (c == '\'')
            {
                i = Add(tokens, TokenKind.StringLiteral, i, PlainStringEnd(text, i + 1));
            }
            else if (c == '"')
            {
                var close = text.IndexOf('"', i + 1);
                i = Add(tokens, TokenKind.QuotedName, i, close < 0 ? text.Length : close + 1);
            }
            else if (char.IsDigit(c) || (c == '.' && char.IsDigit(At(text, i + 1))))
            {
                i = Add(tokens, TokenKind.Number, i, NumberEnd(text, i));
            }
            else if (char.IsLetter(c))
            {
                var stringEnd = StringPrefixEnd(text, i);
                i = stringEnd > 0 ? Add(tokens, TokenKind.StringLiteral, i, stringEnd) : Add(tokens, TokenKind.Word, i, WordEnd(text, i));
            }
            else
            {
                var two = i + 1 < text.Length && Array.Exists(TwoCharSymbols, s => string.CompareOrdinal(text, i, s, 0, 2) == 0);
                i = Add(tokens, TokenKind.Symbol, i, i + (two ? 2 : 1));
            }
        }
        return tokens;
    }

    public static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '$' or '#';

    private static char At(string text, int i) => i < text.Length ? text[i] : '\0';

    private static int Add(List<Token> tokens, TokenKind kind, int start, int end)
    {
        tokens.Add(new Token(kind, start, end - start));
        return end;
    }

    private static int WordEnd(string text, int i)
    {
        while (i < text.Length && IsIdentifierPart(text[i])) i++;
        return i;
    }

    /// <summary>End of '...' whose body starts at <paramref name="i"/>; '' is an escaped quote.</summary>
    private static int PlainStringEnd(string text, int i)
    {
        while (i < text.Length)
        {
            if (text[i] == '\'')
            {
                if (At(text, i + 1) != '\'') return i + 1;
                i++;
            }
            i++;
        }
        return text.Length;
    }

    /// <summary>For N'..', Q'x..x', NQ'x..x' starting at <paramref name="i"/>: the end of the string, otherwise 0.</summary>
    private static int StringPrefixEnd(string text, int i)
    {
        var j = i;
        if (text[j] is 'n' or 'N') j++;
        var q = At(text, j) is 'q' or 'Q';
        if (q) j++;
        if (j == i || At(text, j) != '\'') return 0;
        if (!q) return PlainStringEnd(text, j + 1);
        if (j + 1 >= text.Length) return text.Length;
        var open = text[j + 1];
        var close = open switch { '[' => ']', '{' => '}', '(' => ')', '<' => '>', _ => open };
        for (var k = j + 2; k + 1 < text.Length; k++)
        {
            if (text[k] == close && text[k + 1] == '\'') return k + 2;
        }
        return text.Length;
    }

    private static int NumberEnd(string text, int i)
    {
        while (char.IsDigit(At(text, i))) i++;
        // "1..10" is a range: the dot belongs to "..", not to the number.
        if (At(text, i) == '.' && At(text, i + 1) != '.')
        {
            i++;
            while (char.IsDigit(At(text, i))) i++;
        }
        if (At(text, i) is 'e' or 'E')
        {
            var j = i + 1;
            if (At(text, j) is '+' or '-') j++;
            if (char.IsDigit(At(text, j)))
            {
                i = j;
                while (char.IsDigit(At(text, i))) i++;
            }
        }
        return i;
    }
}
