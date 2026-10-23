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
/// <param name="TightParentheses">No blanks right inside parentheses.</param>
/// <param name="SelectIntoOnOneLine">R15 exception: the SELECT list and the INTO list of SELECT ... INTO, each on one line when it fits.</param>
/// <param name="UpdateSetLayout">R10: <c>SET</c> joined with its first assignment, the <c>=</c> of the SET list aligned.</param>
/// <param name="JoinIfConditions">R11: an IF / ELSIF condition broken over lines is one line again (then wrapped by the margin).</param>
/// <param name="JoinOnAlignment">R12 under JOIN: <c>ON</c> / <c>AND</c> / <c>OR</c> right-aligned to the JOIN keyword.</param>
/// <param name="ShortParenGroupOnOneLine">R14: a parenthesised condition that fits stays on one line.</param>
/// <param name="InsertLayout">R13: INSERT with the column list and the VALUES list in parentheses on their own lines.</param>
/// <param name="HeaderTailsOnOneLine">R18 tail: RESULT_CACHE, AUTHID ..., IS of a subprogram header on the line of the header.</param>
/// <param name="SelectColumnsOnePerLine">R15: one select column per line; the first column after a comment indented like the others.</param>
/// <param name="SubqueryLayout">A subquery in parentheses on one line when it fits, else right after the parenthesis.</param>
/// <param name="TightenAliasPadding">An alias column that would push a line past 120 is not padded.</param>
/// <param name="WrapLongLines">R02: a line past the margin is broken at the outermost call or condition that covers it.</param>
/// <param name="WrapDecodeInPairs">A long DECODE breaks in pairs "condition, value".</param>
/// <param name="WrapBetween">A long <c>x BETWEEN a AND b</c> breaks before AND.</param>
/// <param name="AlignNamedArguments">R09: the <c>=&gt;</c> of named arguments in a broken call are aligned.</param>
/// <param name="AlignDeclarations">Declarations of a declaration section aligned in groups.</param>
/// <param name="IndentComments">A comment line takes the indent of the code it belongs to.</param>
public sealed record FormatOptions(
    int Margin = 76,
    bool BuiltinFunctionsUpper = true,
    bool AttributesUpper = true,
    bool EndWithName = true,
    bool AlignAssignments = true,
    bool BlankLineBeforeSubprograms = true,
    bool NoBlankLineBeforePackageEnd = true,
    bool ExecuteImmediateLiteralOnOwnLine = true,
    bool TrimTrailingSpaces = true,
    bool TightParentheses = true,
    bool SelectIntoOnOneLine = true,
    bool UpdateSetLayout = true,
    bool JoinIfConditions = true,
    bool JoinOnAlignment = true,
    bool ShortParenGroupOnOneLine = true,
    bool InsertLayout = true,
    bool HeaderTailsOnOneLine = true,
    bool SelectColumnsOnePerLine = true,
    bool SubqueryLayout = true,
    bool TightenAliasPadding = true,
    bool WrapLongLines = true,
    bool WrapDecodeInPairs = true,
    bool WrapBetween = true,
    bool AlignNamedArguments = true,
    bool AlignDeclarations = true,
    bool IndentComments = true)
{
    /// <summary>Every rule on, margin 76.</summary>
    public static FormatOptions Default { get; } = new();

    /// <summary>Every rule off: the text stays as it is.</summary>
    public static FormatOptions None { get; } = new(
        Margin: 76, BuiltinFunctionsUpper: false, AttributesUpper: false, EndWithName: false, AlignAssignments: false,
        BlankLineBeforeSubprograms: false, NoBlankLineBeforePackageEnd: false, ExecuteImmediateLiteralOnOwnLine: false, TrimTrailingSpaces: false,
        TightParentheses: false, SelectIntoOnOneLine: false, UpdateSetLayout: false, JoinIfConditions: false, JoinOnAlignment: false,
        ShortParenGroupOnOneLine: false, InsertLayout: false, HeaderTailsOnOneLine: false, SelectColumnsOnePerLine: false, SubqueryLayout: false,
        TightenAliasPadding: false, WrapLongLines: false, WrapDecodeInPairs: false, WrapBetween: false, AlignNamedArguments: false,
        AlignDeclarations: false, IndentComments: false);
}
