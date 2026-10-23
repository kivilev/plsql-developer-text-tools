using System.Text;
using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>
/// The safety net of the post-pass: the code may change in blanks, in line breaks, in the case of keywords and names and
/// in the name after <c>END</c>; anything else (a token, a string literal, a comment) means a rule went wrong and the
/// result must not be used.
/// </summary>
public static class TokenGuard
{
    public static bool Same(string before, string after) =>
        string.Equals(Signature(before), Signature(after), StringComparison.Ordinal);

    /// <summary>Tokens (literals and quoted names exact, the rest in upper case, END names dropped) and the characters of the comments.</summary>
    internal static string Signature(string text)
    {
        var tokens = PlsqlLexer.Tokenize(text);
        var parts = new List<string>(tokens.Count);
        foreach (var t in tokens)
        {
            var s = text.Substring(t.Start, t.Length);
            parts.Add(t.Kind is TokenKind.StringLiteral or TokenKind.QuotedName ? s : s.ToUpperInvariant());
        }

        var sb = new StringBuilder(text.Length);
        var n = 0;
        while (n < parts.Count)
        {
            // END name ; -> END ;  (the name is what the END-name rule adds)
            if (string.Equals(parts[n], "END", StringComparison.Ordinal) && n + 2 < parts.Count
                && tokens[n + 1].Kind == TokenKind.Word && parts[n + 1] is not ("IF" or "LOOP" or "CASE")
                && string.Equals(parts[n + 2], ";", StringComparison.Ordinal))
            {
                sb.Append("END\u0001;\u0001");
                n += 3;
                continue;
            }
            sb.Append(parts[n]).Append('\u0001');
            n++;
        }

        // Comment characters: the mask blanks them, so a character that differs from its mask and is not a blank is a comment character.
        var mask = PlsqlLexer.Mask(text);
        sb.Append('\u0002');
        for (var i = 0; i < text.Length; i++)
        {
            if (mask[i] != text[i] && mask[i] == ' ' && !char.IsWhiteSpace(text[i])) sb.Append(text[i]);
        }
        return sb.ToString();
    }
}
