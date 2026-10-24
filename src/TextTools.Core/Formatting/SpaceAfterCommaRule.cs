using System.Text;

namespace TextTools.Core.Formatting;

/// <summary>A blank after each comma: <c>NVL(1,0)</c> -> <c>NVL(1, 0)</c> (not in literals and comments, not before a closing parenthesis or a comment).</summary>
internal static class SpaceAfterCommaRule
{
    public static string Apply(string text)
    {
        var masked = LineText.Mask(text);
        var sb = new StringBuilder(text.Length + 16);
        for (var i = 0; i < text.Length; i++)
        {
            sb.Append(text[i]);
            if (masked[i] == ',' && i + 1 < text.Length && !char.IsWhiteSpace(masked[i + 1]) && masked[i + 1] != ')' && text[i + 1] != '-' && text[i + 1] != '/') sb.Append(' ');
        }
        return sb.ToString();
    }
}
