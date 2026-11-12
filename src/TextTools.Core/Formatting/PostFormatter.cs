using System.Text;
using System.Text.RegularExpressions;
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

    /// <summary>The rules in the order of the canonical pipeline (cbs_post.post()), each as a guarded step.</summary>
    internal static string Apply(string text, FormatOptions o)
    {
        var steps = new Steps(text);
        // With short arguments kept up to 120 every broken call is joined first; the wrap rule breaks it again where it must.
        steps.Run(o.WrapLongLines, t => JoinCallsRule.Apply(t, o.Margin, o.KeepShortArguments));
        steps.Run(o.SelectColumnsOnePerLine, JsonObjectRule.Apply);
        steps.Run(o.TightParentheses, TightParenthesesRule.Apply);
        steps.Run(o.SpaceAfterComma, SpaceAfterCommaRule.Apply);
        steps.Run(o.BulkCollectOnOneLine, SelectIntoRule.JoinBulkCollect);
        steps.Run(o.TightUnaryMinus, UnaryMinusRule.Apply);
        steps.Run(o.SelectIntoOnOneLine, SelectIntoRule.JoinIntoList);
        steps.Run(o.UpdateSetLayout, UpdateSetRule.AlignEquals);
        steps.Run(o.JoinIfConditions, IfConditionRule.Apply);
        steps.Run(o.ConditionPerLine, IfConditionRule.JoinBooleanExpressions);
        steps.Run(o.JoinOnAlignment, JoinOnRule.Apply);
        steps.Run(o.JsonTableLayout, JsonTableRule.Apply);
        steps.Run(o.JsonArrayaggLayout, JsonObjectRule.NestInArrayagg);
        steps.Run(o.ShortParenGroupOnOneLine, t => ParenConditionRule.Apply(t, o.Margin));
        steps.Run(o.CaseLayout, CaseLayoutRule.JoinShort);
        steps.Run(o.CaseLayout, t => CaseLayoutRule.Relayout(t, o.Margin));
        steps.Run(o.InsertLayout, t => InsertRule.Apply(t, o.Margin));
        steps.Run(o.TypeFieldsLayout, TypeFieldsRule.Apply);
        steps.Run(o.HeaderTailsOnOneLine, HeaderTailsRule.Apply);
        steps.Run(o.SelectColumnsOnePerLine, SelectListRule.SplitLine);
        steps.Run(o.SelectColumnsOnePerLine, SelectListRule.AfterComment);
        steps.Run(o.SubqueryLayout, t => SubqueryRule.Apply(t, o.Margin));
        steps.Run(o.BuiltinFunctionsUpper, CaseRules.UpperBuiltinFunctions);
        steps.Run(o.TightenAliasPadding, t => SelectListRule.TightenAliasPadding(t));
        steps.Run(o.SingleBlankBeforeAlias, t => SelectListRule.UnalignAliases(t));
        steps.Run(o.TableAliasSingleSpace, TableAliasRule.Apply);
        steps.Run(o.TableAliasSingleSpace, TableAliasRule.OnAfterSubquery);
        steps.Run(o.UpdateSetLayout, UpdateSetRule.JoinBareSet);
        steps.Run(o.ExecuteImmediateLiteralOnOwnLine, t => ExecuteImmediateRule.Apply(t, o.Margin));
        steps.Run(o.SelectIntoOnOneLine && !o.SelectListFill, t => SelectIntoRule.JoinShort(t, o.Margin));
        steps.Run(o.SelectListFill, t => SelectListRule.Fill(t, o.Margin));
        steps.Run(o.SelectListFill, t => SelectListRule.FillViewColumns(t, o.Margin));
        steps.Run(o.AlignAssignments, AlignAssignmentsRule.Apply);
        steps.Run(o.AssignmentSingleSpace && !o.AlignAssignments, AlignAssignmentsRule.SingleSpace);
        steps.Run(o.WrapLongLines, t => WrapRule.Apply(t, new WrapRule.Choices(
            o.Margin, o.WrapDecodeInPairs, o.WrapBetween, o.AlignNamedArguments, o.ConditionPerLine, o.FillLiteralArguments, o.WrapInLists, o.KeepShortArguments, o.WrapArithmetic)));
        steps.Run(o.ConditionPerLine, IfConditionRule.ThenOnOwnLine);
        steps.Run(o.JsonArrayaggLayout, JsonObjectRule.JoinFormatJson);
        steps.Run(o.CaseLayout, CaseLayoutRule.ExpandLong);
        steps.Run(o.AlignDeclarations, AlignDeclarationsRule.Apply);
        steps.Run(o.IndentComments, CommentIndentRule.Apply);
        steps.Run(o.OneSpaceBeforeComment, CommentSpaceRule.Apply);
        steps.Run(o.LowerCapitalNames, NameCaseRule.LowerCapitalNames);
        steps.Run(o.UpperContextKeywords, NameCaseRule.UpperContextKeywords);
        steps.Run(o.AttributesUpper, CaseRules.UpperAttributes);
        steps.Run(o.BlankLineBeforeSubprograms || o.BlankLineAfterPackageHeader, t => BlankLineRules.BeforeSubprograms(t, o.BlankLineAfterPackageHeader, o.BlankLineBeforeSubprograms));
        steps.Run(o.NoBlankLineBeforePackageEnd, BlankLineRules.NoBlankBeforePackageEnd);
        steps.Run(o.NoBlankAtBlockEdges, BlankLineRules.NoBlankAtBlockEdges);
        steps.Run(o.EndWithName, EndNameRule.Apply);
        steps.Run(o.TrimTrailingSpaces, TrimTrailing);
        return steps.Text;
    }

    /// <summary>
    /// The text as it moves through the rules. A rule whose result would change anything but layout and case (a token, a string
    /// literal, a comment) is skipped, and so is a rule that fails: one bad rule never costs the other rules, and a literal is never touched.
    /// </summary>
    private sealed class Steps(string text)
    {
        private readonly string _signature = TokenGuard.Signature(text);

        public string Text { get; private set; } = text;

        public void Run(bool enabled, Func<string, string> rule)
        {
            if (!enabled) return;
            string next;
            try
            {
                next = rule(Text);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or IndexOutOfRangeException or RegexMatchTimeoutException)
            {
                return;
            }
            if (string.Equals(next, Text, StringComparison.Ordinal)) return;
            var signature = TokenGuard.Signature(next);
            if (!string.Equals(signature, _signature, StringComparison.Ordinal)) return;
            Text = next;
        }
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
