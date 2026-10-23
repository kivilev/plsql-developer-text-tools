namespace TextTools.Core.Formatting;

/// <summary>
/// The rules of the post-pass that runs after the IDE's own Beautifier, one switch per rule (the "R" numbers are those of the
/// ICBS coding standard that the rules come from, for traceability only). All rules only change layout and the case of keywords
/// and built-in names; <see cref="PostFormatter"/> verifies that the tokens did not change.
/// </summary>
/// <param name="Margin">The right margin that decides what is "long" (the Beautifier wraps at 76).</param>
/// <param name="BuiltinFunctionsUpper">R04: Oracle SQL built-in functions in upper case (<c>nvl(</c> -> <c>NVL(</c>).</param>
/// <param name="AttributesUpper">R06: <c>%TYPE</c>, <c>%ROWTYPE</c>, <c>%FOUND</c> and the like in upper case.</param>
/// <param name="EndWithName">R07: <c>END;</c> of a named subprogram or package gets its name (<c>END open_account;</c>).</param>
/// <param name="AlignAssignments">R08: <c>:=</c> aligned in a group of consecutive assignments.</param>
/// <param name="BlankLineBeforeSubprograms">R19: a blank line before each PROCEDURE/FUNCTION (and the comment above it).</param>
/// <param name="NoBlankLineBeforePackageEnd">R20: no blank line before the closing <c>END</c> at column 0.</param>
/// <param name="ExecuteImmediateLiteralOnOwnLine">R21: a long or multi-line literal of EXECUTE IMMEDIATE starts on its own line, 2 columns deeper.</param>
/// <param name="TrimTrailingSpaces">No trailing blanks (except inside a multi-line string literal).</param>
public sealed record FormatOptions(
    int Margin = 76,
    bool BuiltinFunctionsUpper = true,
    bool AttributesUpper = true,
    bool EndWithName = true,
    bool AlignAssignments = true,
    bool BlankLineBeforeSubprograms = true,
    bool NoBlankLineBeforePackageEnd = true,
    bool ExecuteImmediateLiteralOnOwnLine = true,
    bool TrimTrailingSpaces = true)
{
    /// <summary>Every rule on, margin 76.</summary>
    public static FormatOptions Default { get; } = new();
}
