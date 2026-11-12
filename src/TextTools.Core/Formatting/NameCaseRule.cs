using System.Text;
using System.Text.RegularExpressions;
using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>
/// The case of names and of the keywords the Beautifier does not know. <see cref="LowerCapitalNames"/>: an unquoted name written
/// all in capitals (<c>FROM ACCOUNT A</c>) is written in lower case; mixed case (<c>oJClient</c>) and quoted names are left alone,
/// and so are keywords, Oracle built-ins and the members of Oracle packages. <see cref="UpperContextKeywords"/>: <c>ROWNUM</c>, the
/// words of JSON_TABLE and JSON_OBJECT (<c>PATH '...'</c>, <c>FORMAT JSON</c>, <c>COLUMNS (</c>, <c>KEY '...'</c>), <c>CROSS APPLY</c>
/// and the names of the DBMS_ / UTL_ packages in capitals.
/// </summary>
internal static partial class NameCaseRule
{
    /// <summary>The words that stay in capitals: SQL and PL/SQL keywords, types, predefined exceptions, pseudo-columns, pragmas, collection methods.</summary>
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "ACCESS", "ADD", "AFTER", "AGGREGATE", "ALL", "ALTER", "AND", "ANY", "APPLY", "ARRAY", "AS", "ASC", "AT", "AUTHID",
        "AUTONOMOUS_TRANSACTION", "BEFORE", "BEGIN", "BETWEEN", "BFILE", "BINARY_DOUBLE", "BINARY_FLOAT", "BINARY_INTEGER", "BLOB",
        "BODY", "BOOLEAN", "BOTH", "BULK", "BULK_EXCEPTIONS", "BULK_ROWCOUNT", "BY", "BYTE", "CASCADE", "CASE", "CHAR", "CHARACTER",
        "CHECK", "CLOB", "CLOSE", "CLUSTER", "COLLECT", "COLUMN", "COLUMNS", "COMMENT", "COMMIT", "COMPOUND", "CONNECT", "CONSTANT",
        "CONSTRAINT", "CONTINUE", "COUNT", "CREATE", "CROSS", "CUBE", "CURRENT", "CURRENT_DATE", "CURRENT_TIMESTAMP", "CURRENT_USER",
        "CURRVAL", "CURSOR", "CYCLE", "DATE", "DAY", "DEC", "DECIMAL", "DECLARE", "DEFAULT", "DEFINER", "DELETE", "DELETING",
        "DENSE_RANK", "DESC", "DETERMINISTIC", "DISTINCT", "DO", "DOUBLE", "DROP", "EACH", "EDITIONABLE", "ELSE", "ELSIF", "EMPTY",
        "END", "ERROR", "ERRORS", "ESCAPE", "EXCEPTION", "EXCEPTIONS", "EXCEPTION_INIT", "EXCLUSIVE", "EXECUTE", "EXISTS", "EXIT",
        "EXTEND", "EXTERNAL", "FALSE", "FETCH", "FINAL", "FIRST", "FLOAT", "FOLLOWING", "FOLLOWS", "FOR", "FORALL", "FORCE",
        "FOREIGN", "FORMAT", "FROM", "FULL", "FUNCTION", "GLOBAL", "GOTO", "GRANT", "GROUP", "GROUPING", "HAVING", "HOUR",
        "IDENTIFIED", "IF", "IGNORE", "IMMEDIATE", "IN", "INCLUDING", "INDEX", "INDICES", "INNER", "INSERT", "INSERTING", "INSTEAD",
        "INT", "INTEGER", "INTERSECT", "INTERVAL", "INTO", "IS", "ISOLATION", "JOIN", "JSON", "KEEP", "KEY", "LANGUAGE", "LAST",
        "LEADING", "LEFT", "LEVEL", "LIKE", "LIMIT", "LOCAL", "LOCK", "LOCKED", "LONG", "LOOP", "MATCHED", "MAXVALUE", "MEMBER",
        "MERGE", "MINUS", "MINUTE", "MINVALUE", "MODE", "MONTH", "NATURAL", "NATURALN", "NCHAR", "NCLOB", "NESTED", "NEW", "NEXT",
        "NEXTVAL", "NO", "NOCOPY", "NOCYCLE", "NONEDITIONABLE", "NOT", "NOWAIT", "NULL", "NULLS", "NUMBER", "NUMERIC", "NVARCHAR2",
        "OBJECT", "OF", "OFF", "OFFSET", "OLD", "ON", "ONLY", "OPEN", "OPTION", "OR", "ORDER", "ORDINALITY", "OTHERS", "OUT",
        "OUTER", "OVER", "OVERRIDING", "PACKAGE", "PARALLEL_ENABLE", "PARENT", "PARTITION", "PASSING", "PATH", "PERCENT", "PIPE",
        "PIPELINED", "PIVOT", "PLS_INTEGER", "POSITIVE", "POSITIVEN", "PRAGMA", "PRECEDING", "PRECISION", "PRESERVE", "PRIMARY",
        "PRIOR", "PRIVATE", "PROCEDURE", "PUBLIC", "RAISE", "RANGE", "RAW", "REAL", "RECORD", "REF", "REFERENCES", "REFERENCING",
        "REJECT", "RELIES_ON", "RENAME", "REPLACE", "RESPECT", "RESTRICT_REFERENCES", "RESULT", "RESULT_CACHE", "RETURN",
        "RETURNING", "REVERSE", "REVOKE", "RIGHT", "ROLLBACK", "ROW", "ROWCOUNT", "ROWID", "ROWNUM", "ROWS", "ROWTYPE", "SAMPLE",
        "SAVE", "SAVEPOINT", "SECOND", "SELECT", "SELF", "SEQUENCE", "SERIALLY_REUSABLE", "SET", "SHARE", "SIBLINGS", "SIGNTYPE",
        "SIMPLE_INTEGER", "SIZE", "SKIP", "SMALLINT", "SOME", "SQL", "SQLCODE", "SQLERRM", "START", "STATIC", "SUBTYPE", "SYNONYM",
        "SYSDATE", "SYSTIMESTAMP", "TABLE", "TEMPORARY", "THEN", "TIES", "TIME", "TIMESTAMP", "TIMEZONE_HOUR", "TIMEZONE_MINUTE",
        "TO", "TRAILING", "TRIGGER", "TRIM", "TRUE", "TRUNCATE", "TYPE", "UID", "UNBOUNDED", "UNDER", "UNION", "UNIQUE",
        "UNLIMITED", "UNPIVOT", "UNTIL", "UPDATE", "UPDATING", "UROWID", "USE", "USER", "USING", "VALIDATE", "VALUE", "VALUES",
        "VARCHAR", "VARCHAR2", "VARRAY", "VARYING", "VIEW", "WAIT", "WHEN", "WHENEVER", "WHERE", "WHILE", "WITH", "WITHIN", "WORK",
        "WRAPPED", "WRITE", "XML", "YEAR", "ZONE",
        "ACCESS_INTO_NULL", "CASE_NOT_FOUND", "COLLECTION_IS_NULL", "CURSOR_ALREADY_OPEN", "DUP_VAL_ON_INDEX", "INVALID_CURSOR",
        "INVALID_NUMBER", "LOGIN_DENIED", "NO_DATA_FOUND", "NOT_LOGGED_ON", "PROGRAM_ERROR", "ROWTYPE_MISMATCH", "SELF_IS_NULL",
        "STORAGE_ERROR", "SUBSCRIPT_BEYOND_COUNT", "SUBSCRIPT_OUTSIDE_LIMIT", "SYS_INVALID_ROWID", "TIMEOUT_ON_RESOURCE",
        "TOO_MANY_ROWS", "VALUE_ERROR", "ZERO_DIVIDE",
        "JSON_ARRAY", "JSON_ARRAYAGG", "JSON_EXISTS", "JSON_OBJECT", "JSON_OBJECTAGG", "JSON_QUERY", "JSON_SERIALIZE", "JSON_TABLE",
        "JSON_VALUE", "XMLAGG", "XMLELEMENT", "XMLTYPE", "SYSTIMESTAMP", "LOCALTIMESTAMP", "SESSIONTIMEZONE", "DBTIMEZONE",
        "MAX", "MIN", "SUM", "AVG", "EMPTY_CLOB", "EMPTY_BLOB", "TO_BLOB", "CAST", "MULTISET", "COLLECT",
    };

    private static readonly HashSet<string> OraclePackagePrefixes = new(StringComparer.OrdinalIgnoreCase) { "DBMS_", "UTL_", "APEX_", "OWA_", "HTP" };

    [GeneratedRegex(@"[A-Za-z][\w$#]*", RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex Word();

    [GeneratedRegex(@"(?<![\w$#.])(?:rownum|no_data_found|too_many_rows|dup_val_on_index|value_error|zero_divide|invalid_number|invalid_cursor|cursor_already_open|case_not_found|collection_is_null|subscript_beyond_count|subscript_outside_limit|program_error|storage_error|rowtype_mismatch|access_into_null|self_is_null|login_denied|not_logged_on|timeout_on_resource|sys_invalid_rowid)\b(?!\s*[.(])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex RowNum();

    [GeneratedRegex(@"\b(?:format\s+json|nested\s+path|(?:cross|outer)\s+apply)\b|\bcolumns(?=\s*\()|\b(?:path|key)(?=\s+')", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex JsonWords();

    [GeneratedRegex(@"\b(?:dbms|utl)_[A-Za-z0-9_$#]+(?=\s*\.)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Limits.RegexTimeoutMs)]
    private static partial Regex OraclePackage();

    /// <summary>A name written all in capitals in lower case; keywords, built-ins, the DBMS_ / UTL_ packages and their members stay.</summary>
    public static string LowerCapitalNames(string text)
    {
        var masked = PlsqlLexer.Mask(text);
        var sb = new StringBuilder(text);
        string? previousWord = null;
        var previousEnd = -1;
        foreach (Match m in Word().Matches(masked))
        {
            var w = m.Value;
            var before = m.Index > 0 ? masked[m.Index - 1] : ' ';
            var afterDot = previousWord is not null && previousEnd == m.Index - 1 && before == '.';
            var packageMember = afterDot && OraclePackagePrefixes.Any(p => previousWord!.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            previousWord = w;
            previousEnd = m.Index + m.Length;
            if (before is ':' or '%' or '&') continue; // :NEW, %ROWTYPE, substitution variables
            if (!w.Any(char.IsLetter) || !string.Equals(w, w.ToUpperInvariant(), StringComparison.Ordinal) || string.Equals(w, w.ToLowerInvariant(), StringComparison.Ordinal)) continue;
            if (packageMember || Keywords.Contains(w) || CaseRules.IsBuiltinFunction(w)) continue;
            if (OraclePackagePrefixes.Any(p => w.StartsWith(p, StringComparison.OrdinalIgnoreCase))) continue;
            for (var i = m.Index; i < m.Index + m.Length; i++) sb[i] = char.ToLowerInvariant(sb[i]);
        }
        return sb.ToString();
    }

    /// <summary>ROWNUM, the predefined exceptions (NO_DATA_FOUND, ...), PATH '...', FORMAT JSON, COLUMNS (, KEY '...', NESTED PATH, CROSS / OUTER APPLY and DBMS_x / UTL_x package names in capitals.</summary>
    public static string UpperContextKeywords(string text)
    {
        var masked = PlsqlLexer.Mask(text);
        var sb = new StringBuilder(text);
        foreach (Match m in RowNum().Matches(masked)) Upper(sb, m.Index, m.Length);
        foreach (Match m in OraclePackage().Matches(masked))
        {
            if (m.Index == 0 || !PlsqlLexer.IsIdentifierPart(masked[m.Index - 1]) && masked[m.Index - 1] != '.') Upper(sb, m.Index, m.Length);
        }
        // PATH / KEY followed by a string literal: the literal is masked with 'x', so the quote is looked for in the text itself.
        foreach (Match m in JsonWords().Matches(Unmask(text, masked))) Upper(sb, m.Index, m.Length);
        return sb.ToString();
    }

    /// <summary>The masked text with the opening quote of each string literal put back, so that a pattern can see where a literal starts.</summary>
    private static string Unmask(string text, string masked)
    {
        var chars = masked.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (text[i] == '\'' && chars[i] == 'x' && (i == 0 || chars[i - 1] != 'x')) chars[i] = '\'';
        }
        return new string(chars);
    }

    private static void Upper(StringBuilder sb, int start, int length)
    {
        for (var i = start; i < start + length; i++) sb[i] = char.ToUpperInvariant(sb[i]);
    }
}
