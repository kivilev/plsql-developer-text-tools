using System.Globalization;
using System.Net;
using System.Text;

namespace TextTools.Core.Plan;

/// <summary>
/// The Extended Explain Plan window: an HTML page with the tabs Plan (the tree in reading order and a panel for the
/// selected step), Findings, Text (raw DBMS_XPLAN with a Copy button) and Reference. The IDE shows it in its own HTML
/// window whose engine may be an old Internet Explorer: plain tables and CSS, a little ES3 JavaScript (without it every
/// tab and every step panel is simply shown one after another), no external resources.
/// </summary>
public static class PlanReport
{
    private const string Css = """
        body{font-family:Segoe UI,Tahoma,sans-serif;font-size:13px;margin:8px;color:#1e293b;background:#fff}
        .tabs a{display:inline-block;padding:4px 14px;margin-right:4px;border:1px solid #94a3b8;border-bottom:none;color:#1e293b;text-decoration:none;background:#f1f5f9}
        .tabs a.on{background:#fff;font-weight:bold}
        .tab{border:1px solid #94a3b8;padding:8px}
        pre,.mono{font-family:Consolas,Courier New,monospace;font-size:12px}
        pre{margin:0}
        .error{color:#b91c1c}
        .muted{color:#64748b}
        table{border-collapse:collapse}
        .grid td,.grid th{border-bottom:1px solid #e2e8f0;padding:2px 6px;vertical-align:top;text-align:left;white-space:nowrap}
        .grid tr.step{cursor:pointer}
        .grid tr.sel{background:#e0f2fe}
        .num{text-align:right}
        .bar{display:inline-block;height:8px;background:#94a3b8;margin-right:4px}
        .P{color:#b91c1c;font-weight:bold}
        .C{color:#b45309;font-weight:bold}
        .I{color:#1d4ed8;font-weight:bold}
        .dot{font-size:14px}
        .banner{border:1px solid #fcd34d;background:#fffbeb;padding:4px 8px;margin:4px 0}
        .acc{color:#15803d}
        .flt{color:#b45309}
        .panel{border-left:3px solid #94a3b8;padding:4px 10px;vertical-align:top;white-space:normal}
        .finding{border-bottom:1px solid #e2e8f0;padding:6px 0}
        .course{display:inline-block;margin-top:4px;padding:2px 8px;border:1px solid #1d4ed8;color:#1d4ed8;text-decoration:none}
        .note{margin:2px 0}

        """;

    private const string Script = """
        var tabs=['plan','findings','text','reference'];
        function show(n){for(var i=0;i<tabs.length;i++){var t=document.getElementById(tabs[i]);if(!t)continue;
        t.style.display=tabs[i]==n?'block':'none';document.getElementById('t-'+tabs[i]).className=tabs[i]==n?'on':'';}return false;}
        var current=null;
        function step(id){var d=document.getElementById('d-'+id);if(!d)return false;
        if(current!=null){document.getElementById('d-'+current).style.display='none';document.getElementById('r-'+current).className='step';}
        d.style.display='block';document.getElementById('r-'+id).className='step sel';current=id;show('plan');return false;}
        function init(first){var ds=document.getElementsByTagName('div');for(var i=0;i<ds.length;i++){if(ds[i].className=='detail')ds[i].style.display='none';}
        show('plan');if(first!=null)step(first);}
        function copyText(){var t=document.getElementById('xplan');var s=t.innerText||t.textContent;
        var ok=t.getAttribute('data-ok'),hint=t.getAttribute('data-select');
        if(window.clipboardData&&window.clipboardData.setData){window.clipboardData.setData('Text',s);return msg(ok);}
        try{var r=document.createRange();r.selectNodeContents(t);var sel=window.getSelection();sel.removeAllRanges();sel.addRange(r);
        if(document.execCommand('copy')){sel.removeAllRanges();return msg(ok);}}catch(e){}
        if(navigator.clipboard){navigator.clipboard.writeText(s);return msg(ok);}return msg(hint);}
        function msg(s){document.getElementById('copied').innerHTML=s;return false;}

        """;

    /// <param name="sql">The statement that was explained (constants already resolved).</param>
    /// <param name="analysis">The analysis, or null when there is no plan.</param>
    /// <param name="text">DBMS_XPLAN.DISPLAY lines for the Text tab.</param>
    /// <param name="error">Why there is no plan (not connected, an ORA- error), or null.</param>
    /// <param name="at">When the plan was taken (shown in the footer).</param>
    /// <param name="reference">Texts; the embedded reference by default.</param>
    public static string Html(string sql, PlanAnalysis? analysis, IReadOnlyList<string> text, string? error, DateTimeOffset at, PlanReference? reference = null)
    {
        reference ??= PlanReference.Default;
        var findings = analysis?.Findings ?? [];
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">\n");
        sb.Append("<title>Extended Explain Plan</title>\n<style>\n").Append(Css).Append("</style>\n<script>\n").Append(Script).Append("</script></head>\n");
        var first = analysis?.Tree.Root?.Id;
        sb.Append(CultureInfo.InvariantCulture, $"<body onload=\"init({(first is { } f ? f.ToString(CultureInfo.InvariantCulture) : "null")})\">\n");

        Summary(sb, analysis, error, reference);
        sb.Append("<div class=\"tabs\">");
        Tab(sb, "plan", reference.Ui("tab.plan"));
        Tab(sb, "findings", $"{reference.Ui("tab.findings")} ({findings.Count.ToString(CultureInfo.InvariantCulture)})");
        Tab(sb, "text", reference.Ui("tab.text"));
        Tab(sb, "reference", reference.Ui("tab.reference"));
        sb.Append("</div>\n");

        sb.Append("<div class=\"tab\" id=\"plan\">\n");
        if (error is not null) sb.Append(CultureInfo.InvariantCulture, $"<p class=\"error\">{E(error)}</p>\n");
        sb.Append("<p class=\"muted\">").Append(E(reference.Ui("stmt.label"))).Append("</p><pre>").Append(E(sql)).Append("</pre>\n");
        if (analysis is not null && analysis.Tree.Steps.Count > 0) PlanTab(sb, analysis, reference);
        sb.Append("</div>\n");

        sb.Append("<div class=\"tab\" id=\"findings\">\n");
        FindingsTab(sb, findings, reference);
        sb.Append("</div>\n");

        sb.Append("<div class=\"tab\" id=\"text\">\n");
        if (text.Count > 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<p><a href=\"#\" class=\"course\" onclick=\"return copyText()\">{E(reference.Ui("text.copy"))}</a> <span id=\"copied\" class=\"muted\"></span></p>\n");
            sb.Append(CultureInfo.InvariantCulture, $"<pre id=\"xplan\" data-ok=\"{E(reference.Ui("text.copied"))}\" data-select=\"{E(reference.Ui("text.select"))}\">").Append(E(string.Join("\n", text))).Append("</pre>\n");
        }
        else
        {
            sb.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">{E(reference.Ui("text.none"))}</p>\n");
        }
        sb.Append("</div>\n");

        sb.Append("<div class=\"tab\" id=\"reference\">\n");
        ReferenceTab(sb, analysis, reference);
        sb.Append("</div>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">{E(reference.Ui("footer", ("time", at.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))))}</p>\n</body></html>\n");
        return sb.ToString();
    }

    private static void Tab(StringBuilder sb, string id, string title) =>
        sb.Append(CultureInfo.InvariantCulture, $"<a href=\"#{id}\" id=\"t-{id}\" onclick=\"return show('{id}')\">{E(title)}</a>");

    private static void Summary(StringBuilder sb, PlanAnalysis? analysis, string? error, PlanReference reference)
    {
        if (analysis?.Tree.Root is not { } root)
        {
            if (error is not null) sb.Append(CultureInfo.InvariantCulture, $"<p class=\"error\">{E(error)}</p>\n");
            return;
        }
        string Count(Level l) => analysis.Findings.Count(f => f.Level == l).ToString(CultureInfo.InvariantCulture);
        sb.Append("<p>");
        if (analysis.Text.PlanHash.Length > 0) sb.Append(CultureInfo.InvariantCulture, $"{E(reference.Ui("sum.planHash"))} <b>{E(analysis.Text.PlanHash)}</b> &middot; ");
        sb.Append(CultureInfo.InvariantCulture, $"{E(reference.Ui("sum.cost"))} <b>{E(PlanFormat.Count(root.Row.Cost))}</b> &middot; {E(reference.Ui("sum.rows"))} <b>{E(PlanFormat.Count(root.Row.Rows))}</b> &middot; ");
        sb.Append(CultureInfo.InvariantCulture, $"<span class=\"P\">&#9679; {E(reference.Ui("sum.problems", ("n", Count(Level.Problem))))}</span> ");
        sb.Append(CultureInfo.InvariantCulture, $"<span class=\"C\">&#9679; {E(reference.Ui("sum.check", ("n", Count(Level.Check))))}</span> ");
        sb.Append(CultureInfo.InvariantCulture, $"<span class=\"I\">&#9679; {E(reference.Ui("sum.notes", ("n", Count(Level.Info))))}</span></p>\n");
        foreach (var banner in analysis.Banners)
        {
            if (reference.Banners.TryGetValue(banner, out var t)) sb.Append(CultureInfo.InvariantCulture, $"<div class=\"banner\">{E(t)}</div>\n");
        }
    }

    private static void PlanTab(StringBuilder sb, PlanAnalysis a, PlanReference reference)
    {
        var steps = a.Tree.Steps;
        var maxRows = steps.Max(s => s.Row.Rows ?? 0);
        var maxCost = steps.Max(s => s.Row.Cost ?? 0);
        var byStep = a.Findings.Where(f => f.StepId is not null).ToLookup(f => f.StepId!.Value);
        sb.Append("<table><tr><td style=\"vertical-align:top\">\n<table class=\"grid\"><tr>");
        foreach (var h in new[] { "Step", "Id", "Operation", "Name", "Rows", "Cost", "" })
        {
            var tip = reference.Columns.TryGetValue(h, out var c) ? $" title=\"{E(c)}\"" : "";
            sb.Append(CultureInfo.InvariantCulture, $"<th{tip}>{E(h.Length > 0 ? reference.Ui("h." + h) : h)}</th>");
        }
        sb.Append("</tr>\n");
        foreach (var s in steps)
        {
            var r = s.Row;
            var op = reference.Operation(r);
            var tip = op is null ? "" : $"{op.What}. {op.Normal} {op.Suspicious}".Trim();
            var id = r.Id.ToString(CultureInfo.InvariantCulture);
            var repeat = s.RepeatedFor is null ? "" : $" <span class=\"muted\">&times;{E(PlanFormat.Count(s.RepeatedFor.Row.Rows))}</span>";
            sb.Append(CultureInfo.InvariantCulture, $"<tr class=\"step\" id=\"r-{id}\" onclick=\"step({id})\">");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{s.Order.ToString(CultureInfo.InvariantCulture)}</td><td class=\"num\">{id}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td title=\"{E(tip)}\">{Indent(s.Depth)}{E(r.FullOperation)}{repeat}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td>{E(r.Name)}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{Bar(r.Rows, maxRows)}{E(PlanFormat.Count(r.Rows))}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{Bar(r.Cost, maxCost)}{E(PlanFormat.Count(r.Cost))}</td><td class=\"dot\">");
            foreach (var level in byStep[r.Id].Select(f => f.Level).Distinct().Order())
            {
                sb.Append(CultureInfo.InvariantCulture, $"<span class=\"{Code(level)}\">&#9679;</span>");
            }
            sb.Append("</td></tr>\n");
        }
        sb.Append("</table>\n</td><td class=\"panel\">\n");
        foreach (var s in steps) Detail(sb, a, s, byStep[s.Id], reference);
        sb.Append("</td></tr></table>\n");
    }

    private static void Detail(StringBuilder sb, PlanAnalysis a, PlanStep s, IEnumerable<Finding> findings, PlanReference reference)
    {
        var r = s.Row;
        sb.Append(CultureInfo.InvariantCulture, $"<div class=\"detail\" id=\"d-{r.Id.ToString(CultureInfo.InvariantCulture)}\">");
        sb.Append(CultureInfo.InvariantCulture, $"<p><b>{E(reference.Ui("detail.step", ("n", s.Order.ToString(CultureInfo.InvariantCulture)), ("id", r.Id.ToString(CultureInfo.InvariantCulture))))}</b> {E(r.FullOperation)} {E(r.Name)}</p>");
        sb.Append(CultureInfo.InvariantCulture, $"<p>{E(a.Describe(s))}</p>");
        foreach (var note in s.Annotations) sb.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">{E(note)}</p>");
        if (r.AccessPredicates.Length > 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<p class=\"mono acc\" title=\"{E(reference.Columns.GetValueOrDefault("access", ""))}\">{E(reference.Ui("detail.access"))}: {E(r.AccessPredicates)}</p>");
        }
        if (r.FilterPredicates.Length > 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<p class=\"mono flt\" title=\"{E(reference.Columns.GetValueOrDefault("filter", ""))}\">{E(reference.Ui("detail.filter"))}: {E(r.FilterPredicates)}</p>");
        }
        if (r.PartitionStart.Length > 0) sb.Append(CultureInfo.InvariantCulture, $"<p class=\"mono\">{E(reference.Ui("detail.partitions"))}: {E(r.PartitionStart)} .. {E(r.PartitionStop)}</p>");
        if (reference.Operation(r) is { } op)
        {
            if (op.Normal.Length > 0) sb.Append(CultureInfo.InvariantCulture, $"<p class=\"note\"><b>{E(reference.Ui("detail.normal"))}</b> {E(op.Normal)}</p>");
            if (op.Suspicious.Length > 0) sb.Append(CultureInfo.InvariantCulture, $"<p class=\"note\"><b>{E(reference.Ui("detail.suspicious"))}</b> {E(op.Suspicious)}</p>");
            if (op.Check.Length > 0) sb.Append(CultureInfo.InvariantCulture, $"<p class=\"note\"><b>{E(reference.Ui("detail.check"))}</b> {E(op.Check)}</p>");
        }
        DictionaryFacts(sb, a, s, reference);
        foreach (var f in findings) FindingHtml(sb, f, reference, link: false);
        sb.Append("</div>\n");
    }

    /// <summary>Table size and statistics, indexes with their columns (the used index bold, predicate columns underlined).</summary>
    private static void DictionaryFacts(StringBuilder sb, PlanAnalysis a, PlanStep s, PlanReference reference)
    {
        if (a.TableOf(s) is not { } table || a.Dictionary.Table(table.Owner, table.Name) is not { } t) return;
        var stale = t.Stale ? $" <span class=\"C\">{E(reference.Ui("facts.stale"))}</span>" : "";
        var stats = t.LastAnalyzed.Length == 0
            ? $"<span class=\"P\">{E(reference.Ui("facts.noStats"))}</span>"
            : E(reference.Ui("facts.analyzed", ("date", t.LastAnalyzed))) + stale;
        const string Mark = "\u0001";
        var line = E(reference.Ui("facts.table", ("name", Mark), ("rows", PlanFormat.Count(t.NumRows)), ("blocks", PlanFormat.Count(t.Blocks)), ("stats", Mark + "S")));
        line = line.Replace(Mark + "S", stats, StringComparison.Ordinal).Replace(Mark, $"<b>{E(t.Owner)}.{E(t.Name)}</b>", StringComparison.Ordinal);
        sb.Append("<p>").Append(line);
        if (t.Temporary) sb.Append(", ").Append(E(reference.Ui("facts.temporary")));
        if (t.Partitioned) sb.Append(", ").Append(E(reference.Ui("facts.partitioned", ("key", string.Join(", ", a.Dictionary.PartitionKey(t.Owner, t.Name))))));
        sb.Append("</p>");
        var used = Predicates.Columns(s.Row.AccessPredicates + " " + s.Row.FilterPredicates).Select(c => c.Column).ToHashSet(StringComparer.Ordinal);
        var indexes = a.Dictionary.IndexesOf(t.Owner, t.Name).OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
        if (indexes.Count == 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">{E(reference.Ui("facts.noIndexes"))}</p>");
            return;
        }
        sb.Append(CultureInfo.InvariantCulture, $"<p>{E(reference.Ui("facts.indexes"))}<br>");
        foreach (var i in indexes)
        {
            var name = s.Row.IsIndex && i.Name.Equals(s.Row.Name, StringComparison.Ordinal) ? $"<b>{E(i.Name)}</b>" : E(i.Name);
            var columns = string.Join(", ", i.Columns.Select(c => used.Contains(c) ? $"<u>{E(c)}</u>" : E(c)));
            var state = (i.Unusable ? " UNUSABLE" : "") + (i.Invisible ? " INVISIBLE" : "") + (string.Equals(i.Uniqueness, "UNIQUE", StringComparison.Ordinal) ? " " + reference.Ui("facts.unique") : "");
            sb.Append(CultureInfo.InvariantCulture, $"{name} ({columns}){E(state)}<br>");
        }
        sb.Append("</p>");
    }

    private static void FindingsTab(StringBuilder sb, IReadOnlyList<Finding> findings, PlanReference reference)
    {
        if (findings.Count == 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">{E(reference.Ui("find.none"))}</p>\n");
            return;
        }
        foreach (var f in findings) FindingHtml(sb, f, reference, link: true);
    }

    private static void FindingHtml(StringBuilder sb, Finding f, PlanReference reference, bool link)
    {
        sb.Append(CultureInfo.InvariantCulture, $"<div class=\"finding\"><span class=\"{Code(f.Level)}\">{E(reference.Ui("find." + Code(f.Level)))}</span> <b>{E(f.Title)}</b> <span class=\"muted\">{E(f.RuleId)}</span>");
        if (link && f.StepId is { } id)
        {
            var sid = id.ToString(CultureInfo.InvariantCulture);
            sb.Append(CultureInfo.InvariantCulture, $" &middot; <a href=\"#plan\" onclick=\"return step({sid})\">{E(reference.Ui("find.id", ("id", sid)))}</a>");
        }
        sb.Append(CultureInfo.InvariantCulture, $"<br>{E(f.Detail)}");
        if (f.Normal.Length > 0) sb.Append(CultureInfo.InvariantCulture, $"<br><span class=\"muted\">{E(reference.Ui("find.fine"))} {E(f.Normal)}</span>");
        if (link && f.Level != Level.Info)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<br><a class=\"course\" target=\"_blank\" href=\"{E(reference.CourseLink(f.RuleId))}\">{E(reference.Ui("find.course"))}</a>");
        }
        sb.Append("</div>\n");
    }

    private static void ReferenceTab(StringBuilder sb, PlanAnalysis? a, PlanReference reference)
    {
        sb.Append(CultureInfo.InvariantCulture, $"<h4>{E(reference.Ui("ref.columns"))}</h4><table class=\"grid\">");
        foreach (var (name, text) in reference.Columns)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<tr><td><b>{E(name)}</b></td><td style=\"white-space:normal\">{E(text)}</td></tr>");
        }
        sb.Append("</table>\n");
        var used = a?.Tree.Steps.Select(s => s.Row).Where(r => reference.Operation(r) is not null)
            .Select(r => reference.Operations.ContainsKey(r.FullOperation) ? r.FullOperation : r.Operation).Distinct(StringComparer.Ordinal).ToList() ?? [];
        if (used.Count > 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<h4>{E(reference.Ui("ref.operations"))}</h4><table class=\"grid\">");
            foreach (var name in used)
            {
                var op = reference.Operations[name];
                sb.Append(CultureInfo.InvariantCulture, $"<tr><td><b>{E(name)}</b></td><td style=\"white-space:normal\">{E(op.What)}. {E(op.Normal)} {E(op.Suspicious)} {E(op.Check)}</td></tr>");
            }
            sb.Append("</table>\n");
        }
        var notes = a?.Text.Notes ?? [];
        if (notes.Count > 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<h4>{E(reference.Ui("ref.notes"))}</h4><table class=\"grid\">");
            foreach (var note in notes)
            {
                var info = reference.Note(note);
                sb.Append(CultureInfo.InvariantCulture, $"<tr><td style=\"white-space:normal\"><b>{E(note)}</b></td><td style=\"white-space:normal\">{E(info?.Meaning ?? "")} {E(info?.Action ?? "")}</td></tr>");
            }
            sb.Append("</table>\n");
        }
    }

    private static string Indent(int depth) => string.Concat(Enumerable.Repeat("&nbsp;&nbsp;", depth));

    private static string Bar(long? value, long max) =>
        PlanFormat.LogBar(value, max) is var w and > 0 ? $"<span class=\"bar\" style=\"width:{(w / 2).ToString(CultureInfo.InvariantCulture)}px\"></span>" : "";

    private static string Code(Level l) => l switch
    {
        Level.Problem => "P",
        Level.Check => "C",
        _ => "I",
    };

    private static string E(string s) => WebUtility.HtmlEncode(s);
}
