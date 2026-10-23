using TextTools.Core.Sql;

namespace TextTools.Core.Formatting;

/// <summary>What a character of PL/SQL text is, for colouring it in the settings window preview.</summary>
public enum CodeClass : byte
{
    Plain = 0,
    Keyword = 1,
    Literal = 2,
    Comment = 3,
}

/// <summary>Classifies every character of PL/SQL text: keywords, string and number literals, comments, everything else.</summary>
public static class SyntaxHighlighter
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "all", "alter", "and", "any", "as", "asc", "begin", "between", "body", "by", "case", "close", "commit", "connect", "constant", "create",
        "cursor", "declare", "delete", "desc", "distinct", "drop", "else", "elsif", "end", "exception", "execute", "exists", "exit", "false", "fetch",
        "for", "forall", "from", "function", "grant", "group", "having", "if", "immediate", "in", "inner", "insert", "into", "is", "join", "left",
        "like", "loop", "merge", "minus", "not", "null", "of", "on", "open", "or", "order", "others", "out", "outer", "package", "pragma",
        "procedure", "raise", "record", "replace", "return", "returning", "right", "rollback", "savepoint", "select", "set", "subtype", "table",
        "then", "to", "true", "type", "union", "update", "using", "values", "view", "when", "where", "while", "with",
        "boolean", "date", "integer", "number", "pls_integer", "timestamp", "varchar2", "clob", "blob",
    };

    /// <summary>One <see cref="CodeClass"/> per character of <paramref name="text"/>.</summary>
    public static CodeClass[] Classify(string text)
    {
        var classes = new CodeClass[text.Length];
        var mask = PlsqlLexer.Mask(text);
        // The mask blanks comments: a non-blank character that the mask shows as a blank is a comment character.
        for (var i = 0; i < text.Length; i++)
        {
            if (mask[i] == ' ' && !char.IsWhiteSpace(text[i])) classes[i] = CodeClass.Comment;
        }
        foreach (var t in PlsqlLexer.Tokenize(text))
        {
            var kind = t.Kind switch
            {
                TokenKind.StringLiteral or TokenKind.QuotedName or TokenKind.Number => CodeClass.Literal,
                TokenKind.Word when Keywords.Contains(text.Substring(t.Start, t.Length)) => CodeClass.Keyword,
                _ => CodeClass.Plain,
            };
            if (kind == CodeClass.Plain) continue;
            for (var i = t.Start; i < t.End; i++) classes[i] = kind;
        }
        return classes;
    }
}
