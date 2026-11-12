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
/// <param name="AlignAssignments">R08: <c>:=</c> aligned in a group of consecutive assignments; off by default (the ICBS standard never aligns <c>:=</c>).</param>
/// <param name="BlankLineBeforeSubprograms">R19: a blank line before each PROCEDURE/FUNCTION (and the comment above it).</param>
/// <param name="BlankLineAfterPackageHeader">A blank line between <c>CREATE ... PACKAGE BODY x IS</c> and the first subprogram; off by default (the ICBS standard has none).</param>
/// <param name="NoBlankLineBeforePackageEnd">R20: no blank line before the closing <c>END</c> at column 0.</param>
/// <param name="ExecuteImmediateLiteralOnOwnLine">R21: a long or multi-line literal of EXECUTE IMMEDIATE starts on its own line, 2 columns deeper.</param>
/// <param name="TrimTrailingSpaces">No trailing blanks (except inside a multi-line string literal).</param>
/// <param name="SpaceAfterComma">A blank after each comma (<c>NVL(1,0)</c> -> <c>NVL(1, 0)</c>); on by default: the bank standard writes <c>SUBSTR(x, 1, 3)</c>.</param>
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
/// <param name="TightenAliasPadding">Aligned aliases that push a select list line past 120 characters get one blank instead (the code standard's line limit).</param>
/// <param name="SingleBlankBeforeAlias">The aliases of a select list get one blank before them instead of the column the Beautifier aligns them in.</param>
/// <param name="WrapLongLines">R02: a line past the margin is broken at the outermost call or condition that covers it.</param>
/// <param name="WrapDecodeInPairs">A long DECODE breaks in pairs "condition, value".</param>
/// <param name="WrapBetween">A long <c>x BETWEEN a AND b</c> breaks before AND.</param>
/// <param name="AlignNamedArguments">R09: the <c>=&gt;</c> of named arguments in a broken call are aligned.</param>
/// <param name="AlignDeclarations">Declarations of a declaration section aligned in groups.</param>
/// <param name="IndentComments">A comment line takes the indent of the code it belongs to.</param>
/// <param name="ConditionPerLine">A long condition (IF, ELSIF, WHILE, WHEN, RETURN, <c>:=</c>): one condition per line, AND / OR first; THEN / LOOP of a broken condition on its own line.</param>
/// <param name="FillLiteralArguments">A long call whose arguments are all literals is filled up to the margin instead of one argument per line.</param>
/// <param name="WrapInLists">A long <c>IN (...)</c> list breaks like an argument list.</param>
/// <param name="KeepShortArguments">A call of short positional arguments is not broken while the line is within 120; the short arguments after a long one stay together.</param>
/// <param name="WrapArithmetic">A long <c>a + b - c</c> breaks after its operators.</param>
/// <param name="TightUnaryMinus">A unary minus after <c>(</c> or a comma without a blank: <c>IN (-4068, -4061)</c>.</param>
/// <param name="OneSpaceBeforeComment">Exactly one blank before a comment at the end of a code line.</param>
/// <param name="TableAliasSingleSpace">The alias of a FROM / JOIN source after one blank, not aligned; ON after <c>JOIN (subquery) alias</c> aligned to that JOIN.</param>
/// <param name="NoBlankAtBlockEdges">No blank line right after THEN / ELSE / LOOP / BEGIN nor right before END / ELSE / ELSIF / EXCEPTION.</param>
/// <param name="SelectListFill">The SELECT column list, the INTO list and the column list of a view header packed up to the margin.</param>
/// <param name="JsonArrayaggLayout"><c>JSON_ARRAYAGG(JSON_OBJECT(</c>: JSON_OBJECT on the next line two columns right of JSON_ARRAYAGG; FORMAT JSON at the end of its value.</param>
/// <param name="BulkCollectOnOneLine"><c>BULK COLLECT INTO</c> on one line at the column of INTO.</param>
/// <param name="LowerCapitalNames">A name written all in capitals (<c>FROM ACCOUNT A</c>) in lower case; keywords, built-ins, mixed case and quoted names stay.</param>
/// <param name="CaseLayout">A CASE with one WHEN on one line when it fits in 70; a longer CASE with WHEN / ELSE two columns right of CASE and the value two more.</param>
/// <param name="TypeFieldsLayout">The fields of <c>IS RECORD (</c> / <c>AS OBJECT (</c> four columns right of the header, the closing parenthesis on its own line.</param>
/// <param name="JsonTableLayout">JSON_TABLE: the expression and the path on the line of JSON_TABLE, <c>COLUMNS (</c> under the first argument, one column per line.</param>
/// <param name="AssignmentSingleSpace"><c>x := value</c> with one space on each side of <c>:=</c>, not aligned (when <paramref name="AlignAssignments"/> is off).</param>
/// <param name="UpperContextKeywords"><c>ROWNUM</c>, the JSON_TABLE words (<c>PATH</c>, <c>FORMAT JSON</c>, <c>COLUMNS</c>, <c>KEY</c>), <c>APPLY</c> and DBMS_ / UTL_ package names in capitals.</param>
public sealed record FormatOptions(
    int Margin = 76,
    bool BuiltinFunctionsUpper = true,
    bool AttributesUpper = true,
    bool EndWithName = true,
    bool AlignAssignments = false,
    bool BlankLineBeforeSubprograms = true,
    bool BlankLineAfterPackageHeader = false,
    bool NoBlankLineBeforePackageEnd = true,
    bool ExecuteImmediateLiteralOnOwnLine = true,
    bool TrimTrailingSpaces = true,
    bool SpaceAfterComma = true,
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
    bool SingleBlankBeforeAlias = false,
    bool WrapLongLines = true,
    bool WrapDecodeInPairs = true,
    bool WrapBetween = true,
    bool AlignNamedArguments = true,
    bool AlignDeclarations = true,
    bool IndentComments = true,
    bool ConditionPerLine = true,
    bool FillLiteralArguments = true,
    bool WrapInLists = true,
    bool KeepShortArguments = true,
    bool WrapArithmetic = true,
    bool TightUnaryMinus = true,
    bool OneSpaceBeforeComment = true,
    bool TableAliasSingleSpace = true,
    bool NoBlankAtBlockEdges = true,
    bool SelectListFill = true,
    bool JsonArrayaggLayout = true,
    bool BulkCollectOnOneLine = true,
    bool LowerCapitalNames = true,
    bool UpperContextKeywords = true,
    bool CaseLayout = true,
    bool TypeFieldsLayout = true,
    bool JsonTableLayout = true,
    bool AssignmentSingleSpace = true)
{
    /// <summary>Every rule on, margin 76.</summary>
    public static FormatOptions Default { get; } = new();

    /// <summary>Every rule off: the text stays as it is.</summary>
    public static FormatOptions None { get; } = new(
        Margin: 76, BuiltinFunctionsUpper: false, AttributesUpper: false, EndWithName: false, AlignAssignments: false,
        BlankLineBeforeSubprograms: false, BlankLineAfterPackageHeader: false, NoBlankLineBeforePackageEnd: false, ExecuteImmediateLiteralOnOwnLine: false, TrimTrailingSpaces: false,
        SpaceAfterComma: false, TightParentheses: false, SelectIntoOnOneLine: false, UpdateSetLayout: false, JoinIfConditions: false, JoinOnAlignment: false,
        ShortParenGroupOnOneLine: false, InsertLayout: false, HeaderTailsOnOneLine: false, SelectColumnsOnePerLine: false, SubqueryLayout: false,
        TightenAliasPadding: false, SingleBlankBeforeAlias: false, WrapLongLines: false, WrapDecodeInPairs: false, WrapBetween: false, AlignNamedArguments: false,
        AlignDeclarations: false, IndentComments: false,
        ConditionPerLine: false, FillLiteralArguments: false, WrapInLists: false, KeepShortArguments: false, WrapArithmetic: false, TightUnaryMinus: false, OneSpaceBeforeComment: false, TableAliasSingleSpace: false, NoBlankAtBlockEdges: false, SelectListFill: false, JsonArrayaggLayout: false, BulkCollectOnOneLine: false, LowerCapitalNames: false, UpperContextKeywords: false, CaseLayout: false, TypeFieldsLayout: false, JsonTableLayout: false, AssignmentSingleSpace: false);
}
