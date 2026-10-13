using System.Text;

namespace TextTools.Core;

/// <summary>Text encodings over UTF-8 bytes.</summary>
public static class Encode
{
    public static string ToBase64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    public static string FromBase64(string text) =>
        Decode(() => Convert.FromBase64String(StripWhitespace(text)), "not valid Base64");

    public static string ToUrl(string text) => Uri.EscapeDataString(text);
    public static string FromUrl(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));

    /// <summary>Same as UTL_RAW.CAST_TO_RAW on an AL32UTF8 database: 'Да' → D094D0B0.</summary>
    public static string ToHex(string text) => Convert.ToHexString(Encoding.UTF8.GetBytes(text));
    public static string FromHex(string text)
    {
        var hex = StripWhitespace(text);
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex[2..];
        return Decode(() => Convert.FromHexString(hex), "not valid HEX");
    }

    // Native AOT strips framework exception texts (UseSystemResourceKeys), so give our own.
    private static string Decode(Func<byte[]> bytes, string error)
    {
        try
        {
            return Encoding.UTF8.GetString(bytes());
        }
        catch (FormatException e)
        {
            throw new FormatException(error, e);
        }
    }

    private static string StripWhitespace(string s) => string.Concat(s.Where(c => !char.IsWhiteSpace(c)));
}
