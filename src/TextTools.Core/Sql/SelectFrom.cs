using System.Collections.Frozen;

namespace TextTools.Core.Sql;

/// <summary>
/// <c>select * from &lt;object&gt;</c> for the object name under the cursor (or the selected name): <c>employees</c>,
/// <c>hr.employees</c>, <c>"Mixed Case"</c>, <c>t@dblink</c>. The name is copied as written; nothing is looked up.
/// </summary>
public static class SelectFrom
{
    private const string NoObject = "put the cursor on a table or view name";

    /// <summary>Oracle SQL reserved words and the PL/SQL words a cursor most often lands on: never an object name.</summary>
    private static readonly FrozenSet<string> NotObjects = new[]
    {
        "ACCESS", "ADD", "ALL", "ALTER", "AND", "ANY", "AS", "ASC", "AUDIT", "BETWEEN", "BY", "CHAR", "CHECK", "CLUSTER",
        "COLUMN", "COMMENT", "COMPRESS", "CONNECT", "CREATE", "CURRENT", "DATE", "DECIMAL", "DEFAULT", "DELETE", "DESC",
        "DISTINCT", "DROP", "ELSE", "EXCLUSIVE", "EXISTS", "FILE", "FLOAT", "FOR", "FROM", "GRANT", "GROUP", "HAVING",
        "IDENTIFIED", "IMMEDIATE", "IN", "INCREMENT", "INDEX", "INITIAL", "INSERT", "INTEGER", "INTERSECT", "INTO", "IS",
        "LEVEL", "LIKE", "LOCK", "LONG", "MAXEXTENTS", "MINUS", "MLSLABEL", "MODE", "MODIFY", "NOAUDIT", "NOCOMPRESS",
        "NOT", "NOWAIT", "NULL", "NUMBER", "OF", "OFFLINE", "ON", "ONLINE", "OPTION", "OR", "ORDER", "PCTFREE", "PRIOR",
        "PUBLIC", "RAW", "RENAME", "RESOURCE", "REVOKE", "ROW", "ROWID", "ROWNUM", "ROWS", "SELECT", "SESSION", "SET",
        "SHARE", "SIZE", "SMALLINT", "START", "SUCCESSFUL", "SYNONYM", "SYSDATE", "TABLE", "THEN", "TO", "TRIGGER", "UID",
        "UNION", "UNIQUE", "UPDATE", "USER", "VALIDATE", "VALUES", "VARCHAR", "VARCHAR2", "VIEW", "WHENEVER", "WHERE", "WITH",
        "BEGIN", "END", "DECLARE", "LOOP", "IF", "ELSIF", "WHEN", "CASE", "EXCEPTION", "RETURN", "RETURNING", "PROCEDURE",
        "FUNCTION", "PACKAGE", "BODY", "TYPE", "CURSOR", "CONSTANT", "WHILE", "EXIT", "RAISE", "OTHERS", "JOIN", "LEFT",
        "RIGHT", "INNER", "OUTER", "FULL", "CROSS", "USING", "MERGE", "BULK", "COLLECT", "FORALL", "EXECUTE", "COMMIT",
        "ROLLBACK", "NULLS", "FETCH", "OPEN", "CLOSE", "REPLACE", "TRUE", "FALSE",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static CopyResult Build(SqlRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = request.SelectionLength > 0 ? Selected(request) : UnderCursor(request.Text, request.Cursor);
        var sql = "select * from " + name;
        return new CopyResult(sql, sql);
    }

    /// <summary>The selection must be one object name (spaces around it are ignored).</summary>
    private static string Selected(SqlRequest r)
    {
        var text = r.Text;
        if (r.SelectionStart < 0 || r.SelectionStart > text.Length - r.SelectionLength)
        {
            throw new FormatException("could not locate the selection, select a unique fragment");
        }
        var selected = text.Substring(r.SelectionStart, r.SelectionLength).Trim();
        var tokens = PlsqlLexer.Tokenize(selected);
        if (tokens.Count == 0 || !IsName(selected, tokens, 0)) throw new FormatException("select a table or view name");
        var (from, to) = Chain(selected, tokens, 0);
        if (from != 0 || to != tokens.Count) throw new FormatException("select a table or view name");
        return Name(selected, tokens, from, to);
    }

    private static string UnderCursor(string text, int cursor)
    {
        var tokens = PlsqlLexer.Tokenize(text);
        int inside = -1, after = -1;
        for (var k = 0; k < tokens.Count && tokens[k].Start <= cursor; k++)
        {
            if (cursor < tokens[k].End) inside = k;
            else if (cursor == tokens[k].End && IsName(text, tokens, k)) after = k;
        }
        // In a name, else right after one (the cursor at the end of the word, before ';' or ' ').
        var at = inside;
        if (!IsName(text, tokens, inside) && after >= 0) at = after;
        if (at < 0) throw new FormatException(NoObject);
        // On the '.' or '@' between two parts: the part before it.
        if (!IsName(text, tokens, at) && IsSeparator(text, tokens, at) && at > 0 && IsName(text, tokens, at - 1)) at--;
        if (!IsName(text, tokens, at)) throw new FormatException(NoObject);

        var (from, to) = Chain(text, tokens, at);
        // :new.x, :bind and &substitution are not objects.
        if (from > 0 && Adjacent(tokens, from - 1, from) && text[tokens[from - 1].Start] is ':' or '&')
        {
            throw new FormatException(NoObject);
        }
        return Name(text, tokens, from, to);
    }

    /// <summary>The longest run name ('.' | '@') name ... around token <paramref name="at"/>, without spaces inside.</summary>
    private static (int From, int To) Chain(string text, IReadOnlyList<Token> tokens, int at)
    {
        int from = at, to = at + 1;
        while (from >= 2 && IsSeparator(text, tokens, from - 1) && IsName(text, tokens, from - 2)
               && Adjacent(tokens, from - 2, from - 1) && Adjacent(tokens, from - 1, from))
        {
            from -= 2;
        }
        while (to + 1 < tokens.Count && IsSeparator(text, tokens, to) && IsName(text, tokens, to + 1)
               && Adjacent(tokens, to - 1, to) && Adjacent(tokens, to, to + 1))
        {
            to += 2;
        }
        return (from, to);
    }

    private static string Name(string text, IReadOnlyList<Token> tokens, int from, int to)
    {
        if (to - from == 1 && tokens[from].Kind == TokenKind.Word
            && NotObjects.Contains(text.Substring(tokens[from].Start, tokens[from].Length).ToUpperInvariant()))
        {
            throw new FormatException(NoObject);
        }
        return text[tokens[from].Start..tokens[to - 1].End];
    }

    private static bool IsName(string text, IReadOnlyList<Token> tokens, int k) =>
        k >= 0 && k < tokens.Count
        && (tokens[k].Kind == TokenKind.Word
            || (tokens[k].Kind == TokenKind.QuotedName && tokens[k].Length >= 3 && text[tokens[k].End - 1] == '"'));

    private static bool IsSeparator(string text, IReadOnlyList<Token> tokens, int k) =>
        k >= 0 && k < tokens.Count && tokens[k].Kind == TokenKind.Symbol && tokens[k].Length == 1 && text[tokens[k].Start] is '.' or '@';

    private static bool Adjacent(IReadOnlyList<Token> tokens, int a, int b) => tokens[a].End == tokens[b].Start;
}
