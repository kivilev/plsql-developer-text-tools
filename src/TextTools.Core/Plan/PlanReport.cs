using System.Globalization;
using System.Net;
using System.Text;

namespace TextTools.Core.Plan;

/// <summary>A note about the plan: how serious, a short title, the explanation in plain words.</summary>
public sealed record Finding(string Severity, string Title, string Detail);

/// <summary>
/// The Extended Explain Plan window: an HTML page with two tabs, "Plan" (DBMS_XPLAN output) and "Findings".
/// The IDE shows it in its own HTML window whose engine may be an old Internet Explorer: plain tables and CSS, a few
/// lines of JavaScript to switch tabs (without JavaScript both tabs are simply shown one after another), no external
/// resources.
/// </summary>
public static class PlanReport
{
    /// <summary>Notes for the Findings tab. For now a few simple checks over the plan text plus test notes.</summary>
    public static IReadOnlyList<Finding> Findings(IReadOnlyList<string> plan)
    {
        var findings = new List<Finding>();
        if (plan.Count == 0) return findings;
        foreach (var line in plan)
        {
            if (line.Contains("TABLE ACCESS FULL", StringComparison.OrdinalIgnoreCase)
                || line.Contains("TABLE ACCESS STORAGE FULL", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new Finding("warning", "Full table scan (test rule)",
                    $"The whole table is read: {Squeeze(line)}. Fine for a small table; for a big one check the indexes on the WHERE columns."));
            }
            else if (line.Contains("MERGE JOIN CARTESIAN", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new Finding("warning", "Cartesian join (test rule)",
                    "Every row is joined with every row: usually a join condition is missing."));
            }
        }
        findings.Add(new Finding("info", "Test finding", "This tab will explain the plan in plain words. These notes are placeholders for now."));
        return findings;
    }

    /// <param name="sql">The statement that was explained (constants already resolved).</param>
    /// <param name="plan">DBMS_XPLAN.DISPLAY lines; empty when there is no plan.</param>
    /// <param name="error">Why there is no plan (not connected, an ORA- error), or null.</param>
    /// <param name="findings">Notes for the Findings tab.</param>
    /// <param name="at">When the plan was taken (shown in the footer).</param>
    public static string Html(string sql, IReadOnlyList<string> plan, string? error, IReadOnlyList<Finding> findings, DateTimeOffset at)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">\n");
        sb.Append("<title>Extended Explain Plan</title>\n<style>\n");
        sb.Append("body{font-family:Segoe UI,Tahoma,sans-serif;font-size:13px;margin:8px;color:#1e293b;background:#fff}\n");
        sb.Append(".tabs a{display:inline-block;padding:4px 14px;margin-right:4px;border:1px solid #94a3b8;border-bottom:none;");
        sb.Append("color:#1e293b;text-decoration:none;background:#f1f5f9}\n.tabs a.on{background:#fff;font-weight:bold}\n");
        sb.Append(".tab{border:1px solid #94a3b8;padding:8px}\npre{font-family:Consolas,Courier New,monospace;font-size:12px;margin:0}\n");
        sb.Append(".error{color:#b91c1c}\n.muted{color:#64748b}\ntable{border-collapse:collapse}\n");
        sb.Append("td{border-bottom:1px solid #e2e8f0;padding:4px 8px;vertical-align:top}\n.warning{color:#b45309;font-weight:bold}\n");
        sb.Append(".info{color:#0e7490;font-weight:bold}\n</style>\n<script>\n");
        sb.Append("function show(n){var t=['plan','findings'];for(var i=0;i<t.length;i++){");
        sb.Append("document.getElementById(t[i]).style.display=t[i]==n?'block':'none';");
        sb.Append("document.getElementById('t-'+t[i]).className=t[i]==n?'on':'';}return false;}\n");
        sb.Append("</script></head>\n<body onload=\"show('plan')\">\n<div class=\"tabs\">");
        sb.Append(CultureInfo.InvariantCulture, $"<a href=\"#plan\" id=\"t-plan\" class=\"on\" onclick=\"return show('plan')\">Plan</a>");
        sb.Append(CultureInfo.InvariantCulture, $"<a href=\"#findings\" id=\"t-findings\" onclick=\"return show('findings')\">Findings ({findings.Count})</a></div>\n");

        sb.Append("<div class=\"tab\" id=\"plan\">\n");
        if (error is not null) sb.Append(CultureInfo.InvariantCulture, $"<p class=\"error\">{E(error)}</p>\n");
        if (plan.Count > 0) sb.Append("<pre>").Append(E(string.Join("\n", plan))).Append("</pre>\n");
        sb.Append("<p class=\"muted\">Statement:</p><pre>").Append(E(sql)).Append("</pre>\n</div>\n");

        sb.Append("<div class=\"tab\" id=\"findings\">\n");
        if (findings.Count == 0)
        {
            sb.Append("<p class=\"muted\">No findings.</p>\n");
        }
        else
        {
            sb.Append("<table>\n");
            foreach (var f in findings)
            {
                sb.Append(CultureInfo.InvariantCulture, $"<tr><td class=\"{E(f.Severity)}\">{E(f.Severity)}</td><td><b>{E(f.Title)}</b><br>{E(f.Detail)}</td></tr>\n");
            }
            sb.Append("</table>\n");
        }
        sb.Append("</div>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">Text Tools for PL/SQL Developer, {at:yyyy-MM-dd HH:mm:ss}</p>\n</body></html>\n");
        return sb.ToString();
    }

    private static string E(string s) => WebUtility.HtmlEncode(s);

    private static string Squeeze(string line) => string.Join(' ', line.Split(['|', ' '], StringSplitOptions.RemoveEmptyEntries));
}
