using System.Globalization;
using System.Text.RegularExpressions;

namespace TextTools.Core.Plan;

/// <summary>A hint the optimizer reported as a problem: E = syntax error, N = unresolved, U = unused; the reason after " / ".</summary>
public sealed record HintIssue(int? Step, char Code, string Hint, string Reason);

/// <summary>
/// What only the DBMS_XPLAN text has (or has in the most stable form across versions): the Note section, the Hint
/// Report (19c+), the plan hash and the inactive rows of an adaptive plan (marked "-" with +ADAPTIVE).
/// </summary>
public sealed partial class PlanText
{
    private PlanText(string planHash, IReadOnlyList<string> notes, IReadOnlyList<HintIssue> hints, IReadOnlySet<int> inactive)
    {
        PlanHash = planHash;
        Notes = notes;
        Hints = hints;
        Inactive = inactive;
    }

    public string PlanHash { get; }

    /// <summary>The Note lines without the leading "- ", wrapped lines joined.</summary>
    public IReadOnlyList<string> Notes { get; }

    public IReadOnlyList<HintIssue> Hints { get; }

    public IReadOnlySet<int> Inactive { get; }

    public static PlanText Parse(IReadOnlyList<string> lines)
    {
        var hash = "";
        var notes = new List<string>();
        var hints = new List<HintIssue>();
        var inactive = new HashSet<int>();
        var section = "";
        int? hintStep = null;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("Plan hash value:", StringComparison.Ordinal))
            {
                hash = line["Plan hash value:".Length..].Trim();
                continue;
            }
            if (InactiveRow().Match(line) is { Success: true } m)
            {
                inactive.Add(int.Parse(m.Groups["id"].Value, CultureInfo.InvariantCulture));
                continue;
            }
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.StartsWith('-') && !line.StartsWith('|'))
            {
                section = Section(line, section);
                continue;
            }
            if (string.Equals(section, "note", StringComparison.Ordinal))
            {
                var t = line.Trim();
                if (t.StartsWith("- ", StringComparison.Ordinal)) notes.Add(t[2..].Trim());
                else if (t.Length > 0 && notes.Count > 0 && !t.All(c => c == '-')) notes[^1] += " " + t;
            }
            else if (string.Equals(section, "hint", StringComparison.Ordinal))
            {
                if (HintStep().Match(line) is { Success: true } s) hintStep = int.Parse(s.Groups["id"].Value, CultureInfo.InvariantCulture);
                else if (HintCode().Match(line) is { Success: true } h) hints.Add(Hint(hintStep, h.Groups["code"].Value[0], h.Groups["hint"].Value));
            }
        }
        return new PlanText(hash, notes, hints, inactive);
    }

    private static HintIssue Hint(int? step, char code, string text)
    {
        var slash = text.IndexOf(" / ", StringComparison.Ordinal);
        return slash < 0 ? new HintIssue(step, code, text.Trim(), "") : new HintIssue(step, code, text[..slash].Trim(), text[(slash + 3)..].Trim());
    }

    private static string Section(string header, string current)
    {
        if (header.StartsWith("Note", StringComparison.Ordinal)) return "note";
        if (header.StartsWith("Hint Report", StringComparison.Ordinal)) return "hint";
        return header.StartsWith("Total hints", StringComparison.Ordinal) ? current : "";
    }

    [GeneratedRegex(@"^\|-\s*\*?\s*(?<id>\d+)\s*\|", RegexOptions.ExplicitCapture, Limits.RegexTimeoutMs)]
    private static partial Regex InactiveRow();

    [GeneratedRegex(@"^\s*(?<id>\d+)\s+-\s", RegexOptions.ExplicitCapture, Limits.RegexTimeoutMs)]
    private static partial Regex HintStep();

    [GeneratedRegex(@"^\s*(?<code>[EUN])\s+-\s+(?<hint>\S.*)$", RegexOptions.ExplicitCapture, Limits.RegexTimeoutMs)]
    private static partial Regex HintCode();
}
