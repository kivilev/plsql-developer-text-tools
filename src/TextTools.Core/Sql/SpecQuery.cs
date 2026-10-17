namespace TextTools.Core.Sql;

/// <summary>
/// Dictionary queries for package specifications. Each one is a plain indexed lookup (owner + name): no ALL_OBJECTS,
/// no joins - on a big database those take seconds. Names come from parsed identifiers; quotes are doubled anyway,
/// so the text cannot break out of the literal.
/// </summary>
public static class SpecQuery
{
    private const string CurrentSchema = "sys_context('USERENV', 'CURRENT_SCHEMA')";

    /// <summary>Source lines of the specification: owner.package from ALL_SOURCE, a bare package from USER_SOURCE.</summary>
    public static string Source(string? owner, string package) => owner is null
        ? $"select text from user_source where name = {Literal(package)} and type = 'PACKAGE' order by line"
        : $"select text from all_source where owner = {Literal(owner)} and name = {Literal(package)} and type = 'PACKAGE' order by line";

    /// <summary>"OWNER.NAME" a synonym points to: a private synonym of the current schema first, then a public one.</summary>
    public static string Synonym(string name) =>
        $"select table_owner || '.' || table_name from all_synonyms where synonym_name = {Literal(name)}"
        + $" and owner in ({CurrentSchema}, 'PUBLIC') order by case owner when 'PUBLIC' then 2 else 1 end";

    /// <summary>Splits a <see cref="Synonym"/> row; null when it is not OWNER.NAME.</summary>
    public static (string Owner, string Name)? ParseTarget(string? row)
    {
        var dot = row?.IndexOf('.', StringComparison.Ordinal) ?? -1;
        return dot > 0 && dot < row!.Length - 1 ? (row[..dot], row[(dot + 1)..]) : null;
    }

    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
