using System.Text;
using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>
/// The post-pass after the IDE's Beautifier: layout and case rules that the Beautifier cannot express (see
/// <see cref="FormatOptions"/>). A rule works on lines and on the same text with comments and literals masked
/// (<see cref="PlsqlLexer.Mask"/>), so it can never match inside either. After the rules the tokens are compared with the
/// input (<see cref="TokenGuard"/>): if anything but case, blanks and END names changed, the input is returned untouched.
/// </summary>
public static class PostFormatter
{
    private static volatile FormatOptions _current = FormatOptions.Default;

    /// <summary>The options the menu command runs with: the saved profile, set by the plug-in at start and after the settings window saves.</summary>
    public static FormatOptions Current
    {
        get => _current;
        set => _current = value ?? FormatOptions.Default;
    }

    /// <summary>The text after the enabled rules, or the input itself when it would not be safe to change it.</summary>
    public static string Format(string text, FormatOptions? options = null) =>
        TryFormat(text, options, out var result) ? result : text;

    /// <summary>False when the rules would have changed more than layout and case: <paramref name="result"/> is then the input.</summary>
    public static bool TryFormat(string text, FormatOptions? options, out string result)
    {
        options ??= FormatOptions.Default;
        result = text;
        if (string.IsNullOrWhiteSpace(text)) return true;

        // Work with \n; give the text its own line ends back (CRLF only when every line end was CRLF).
        var crlf = text.Contains("\r\n", StringComparison.Ordinal) && !text.Replace("\r\n", "", StringComparison.Ordinal).Contains('\n', StringComparison.Ordinal);
        var work = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var formatted = Apply(work, options);
        if (!TokenGuard.Same(work, formatted)) return false;
        result = crlf ? formatted.Replace("\n", "\r\n", StringComparison.Ordinal) : formatted;
        return true;
    }

    /// <summary>The rules in the order of the canonical pipeline; no guard (tests of single rules use it).</summary>
    internal static string Apply(string text, FormatOptions o)
    {
        if (o.BuiltinFunctionsUpper) text = CaseRules.UpperBuiltinFunctions(text);
        if (o.ExecuteImmediateLiteralOnOwnLine) text = ExecuteImmediateRule.Apply(text, o.Margin);
        if (o.AlignAssignments) text = AlignAssignmentsRule.Apply(text);
        if (o.AttributesUpper) text = CaseRules.UpperAttributes(text);
        if (o.BlankLineBeforeSubprograms) text = BlankLineRules.BeforeSubprograms(text);
        if (o.NoBlankLineBeforePackageEnd) text = BlankLineRules.NoBlankBeforePackageEnd(text);
        if (o.EndWithName) text = EndNameRule.Apply(text);
        if (o.TrimTrailingSpaces) text = TrimTrailing(text);
        return text;
    }

    /// <summary>Drops trailing blanks, except on a line whose end is inside a multi-line string literal.</summary>
    internal static string TrimTrailing(string text)
    {
        var lines = text.Split('\n');
        var masked = PlsqlLexer.Mask(text).Split('\n');
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            var inLiteral = masked[i].Length > 0 && masked[i][^1] == 'x';
            sb.Append(inLiteral ? lines[i] : lines[i].TrimEnd(' ', '\t'));
        }
        return sb.ToString();
    }
}
