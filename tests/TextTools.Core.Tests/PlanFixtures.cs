using System.Text;
using TextTools.Core.Plan;

namespace TextTools.Core.Tests;

/// <summary>
/// Loads a plan captured from a real Oracle 23ai (Plans/*.txt): the statement, PLAN_TABLE rows, the adaptive display
/// map, dictionary rows and the DBMS_XPLAN text, in the column order of <see cref="PlanSql"/>. Tabular sections are
/// SQL*Plus CSV (quoted, "" inside quotes), parsed by the same functions the plug-in uses.
/// </summary>
internal static class PlanFixtures
{
    public static (string Sql, PlanData Data) Load(string name)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Plans", name + ".txt"));
        var sections = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? current = null;
        foreach (var line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                current = sections[line[3..].Trim()] = [];
            }
            else if (current is not null && (line.Length > 0 || string.Equals(Key(sections, current), "text", StringComparison.Ordinal)))
            {
                current.Add(line);
            }
        }
        List<string> Section(string key) => sections.GetValueOrDefault(key) ?? [];
        IEnumerable<string[]> Csv(string key) => Section(key).Select(ParseCsv);

        var rows = Csv("rows").Select(PlanSql.ParseRow).OfType<PlanRow>().ToList();
        var map = Csv("map").Select(PlanSql.ParseDisplayMap).OfType<DisplayMapRow>().ToList();
        var catalog = new PlanCatalog(
            Csv("tables").Select(PlanSql.ParseTable).OfType<TableInfo>(),
            Csv("indexes").Select(PlanSql.ParseIndex).OfType<IndexInfo>(),
            Csv("columns").Select(r => (r[0], r[1], r[2], r[3])),
            Csv("partkeys").Select(r => (r[0], r[1], r[2])),
            []);
        var text = Section("text").SkipWhile(l => l.Length == 0).ToList();
        return (string.Join(" ", Section("sql")), new PlanData(rows, map, text, catalog));
    }

    public static PlanAnalysis Analyze(string name)
    {
        var (sql, data) = Load(name);
        return PlanAnalysis.Analyze(data, sql);
    }

    private static string Key(Dictionary<string, List<string>> sections, List<string> list) =>
        sections.First(kv => ReferenceEquals(kv.Value, list)).Key;

    /// <summary>One SQL*Plus CSV line: "a","b""c",,"d" -> [a, b"c, "", d].</summary>
    public static string[] ParseCsv(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        var i = 0;
        while (i < line.Length)
        {
            var c = line[i++];
            if (quoted)
            {
                if (c != '"') sb.Append(c);
                else if (i < line.Length && line[i] == '"') sb.Append(line[i++]);
                else quoted = false;
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return [.. fields];
    }
}
