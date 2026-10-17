using System.Text;

namespace TextTools.Core.Sql;

/// <summary>The editor text with the selection (offset, length; length 0 = none) and the cursor offset.</summary>
public sealed record SqlRequest(string Text, int SelectionStart, int SelectionLength, int Cursor);

/// <summary>Text for the clipboard and a one-line summary for the status bar.</summary>
public sealed record CopyResult(string Text, string Summary);

/// <summary>Package specifications that are not in the editor text: the database the IDE is connected to.</summary>
public interface ISpecSource
{
    /// <summary>The source text of the package specification, or null when there is no such package.</summary>
    string? PackageSpec(string? owner, string package);
}

/// <summary>
/// Turns a SQL statement written inside PL/SQL into one that runs on its own (SQL Window, Explain Plan):
/// PL/SQL constants become their values, variables and parameters become bind variables (:name),
/// INTO / BULK COLLECT INTO of a query and RETURNING ... INTO of DML are removed.
/// Constants are looked up like PL/SQL does: the enclosing blocks from the inside out, then the specification of the
/// package whose body contains the statement; <c>pkg.c_x</c> and <c>owner.pkg.c_x</c> in package specifications from
/// the same text, then from <see cref="ISpecSource"/> (the database). A constant whose value cannot be found or used
/// in SQL becomes a bind variable named like the constant (<c>pkg.c_x</c> -> <c>:c_x</c>).
/// </summary>
public static class ConstantResolver
{
    private const int MaxDepth = 10;
    private const int MaxSpecLookups = 50;
    private const int MaxListedUnresolved = 5;

    public static CopyResult CopySql(SqlRequest request) => CopySql(request, null);

    public static CopyResult CopySql(SqlRequest request, ISpecSource? specs) => new Run(request.Text, specs).Copy(request);

    private readonly record struct Edit(int Start, int End, string Replacement);

    private sealed class Run(string text, ISpecSource? specs)
    {
        private readonly PlsqlSource _window = new(text);
        private readonly Dictionary<string, PlsqlSource?> _specs = new(StringComparer.Ordinal);
        private readonly List<string> _unresolved = [];
        private readonly List<(int Start, int End)> _removed = [];
        private readonly List<(int Start, int End)> _columnLists = [];
        private readonly HashSet<string> _columns = new(StringComparer.Ordinal);
        private readonly HashSet<string> _tables = new(StringComparer.Ordinal);
        private readonly List<(int Start, int End)> _tableNames = [];
        private readonly Dictionary<string, string> _bindNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _constantBinds = [];
        private int _constants;
        private int _binds;

        public CopyResult Copy(SqlRequest r)
        {
            var (start, end) = Range(r);
            var tokens = _window.Tokens;
            int from = 0, to = tokens.Count;
            while (from < to && tokens[from].Start < start) from++;
            while (to > from && tokens[to - 1].End > end) to--;
            if (from == to) throw new FormatException("nothing to copy: select a SQL statement or put the cursor in one");

            FindRemovals(from, to);
            FindInsertColumnLists(from, to);
            var (statementFrom, statementTo) = EnclosingStatement(from, to);
            FindTables(statementFrom, statementTo);
            for (var k = from; k < to; k++)
            {
                if (_window.IsName(k) && IsSetTarget(_window, k)) _columns.Add(_window.Name(k));
                if (_window.IsKeyword(k, "SET") && _window.IsSymbol(k + 1, "("))
                {
                    for (var c = k + 2; c < to && !_window.IsSymbol(c, ")"); c++)
                    {
                        if (_window.IsName(c)) _columns.Add(_window.Name(c));
                    }
                }
            }
            var edits = new List<Edit>();
            Substitute(_window, from, to, statement: true, 0, [], edits);
            var sql = Apply(_window.Text, start, end, edits);
            return new CopyResult(sql, Summary());
        }

        /// <summary>The selection, or the statement under the cursor (without its terminating ';').</summary>
        private (int Start, int End) Range(SqlRequest r)
        {
            if (r.SelectionLength > 0)
            {
                if (r.SelectionStart < 0 || r.SelectionStart + r.SelectionLength > _window.Text.Length)
                {
                    throw new FormatException("could not locate the selection, select a unique fragment");
                }
                return (r.SelectionStart, r.SelectionStart + r.SelectionLength);
            }
            foreach (var (from, to, until) in Statements())
            {
                if (_window.Tokens[from].Start <= r.Cursor && r.Cursor <= until)
                {
                    return (_window.Tokens[from].Start, _window.Tokens[to - 1].End);
                }
            }
            // The cursor after the ';' at the end of the line (trailing spaces) still means that statement.
            foreach (var (from, to, until) in Statements().Reverse())
            {
                if (until <= r.Cursor && r.Cursor <= _window.Text.Length && _window.Text.AsSpan(until, r.Cursor - until).Trim(" \t").IsEmpty)
                {
                    return (_window.Tokens[from].Start, _window.Tokens[to - 1].End);
                }
            }
            throw new FormatException("put the cursor in a SQL statement (SELECT, WITH, INSERT, UPDATE, DELETE, MERGE) or select one");
        }

        /// <summary>
        /// SQL statements as token ranges [From, To) and the offset where the statement ends (after ';' if any).
        /// A statement starts with a DML keyword where a PL/SQL statement can start and ends at ';', at a '/' line,
        /// or at the ')' that closes the bracket it started in (FOR r IN (SELECT ...)).
        /// </summary>
        private IEnumerable<(int From, int To, int Until)> Statements()
        {
            var s = _window;
            var i = 0;
            while (i < s.Tokens.Count)
            {
                if (!StartsStatement(i))
                {
                    i++;
                    continue;
                }
                var depth = 0;
                var j = i;
                for (; j < s.Tokens.Count; j++)
                {
                    if (s.IsSymbol(j, "(")) depth++;
                    else if ((s.IsSymbol(j, ")") && --depth < 0) || s.IsSymbol(j, ";") || IsSlashLine(j)) break;
                }
                var until = j < s.Tokens.Count ? s.Tokens[j].End : s.Text.Length;
                if (j > i) yield return (i, j, until);
                i = j + 1;
            }
        }

        private bool StartsStatement(int i)
        {
            var s = _window;
            var p = i - 1;
            switch (s.Keyword(i))
            {
                case "SELECT" or "WITH":
                    return i == 0 || s.IsSymbol(p, ";") || s.IsSymbol(p, ">>") || IsSlashLine(p)
                           || (s.IsSymbol(p, "(") && s.IsKeyword(p - 1, "IN"))
                           || s.Keyword(p) is "BEGIN" or "THEN" or "ELSE" or "LOOP" or "DECLARE" or "IS" or "AS" or "FOR";
                case "INSERT" or "UPDATE" or "DELETE" or "MERGE":
                    // Not a collection method (l_tab.delete), a trigger event (BEFORE INSERT OR UPDATE OF) or a privilege.
                    return i == 0 || (!s.IsSymbol(p, ".") && !s.IsSymbol(p, ",")
                                      && s.Keyword(p) is not ("BEFORE" or "AFTER" or "OR" or "OF" or "FOR" or "ON" or "GRANT" or "REVOKE"));
                default:
                    return false;
            }
        }

        private bool IsSlashLine(int i)
        {
            if (!_window.IsSymbol(i, "/")) return false;
            var t = _window.Tokens[i];
            var all = _window.Text;
            for (var k = t.Start - 1; k >= 0 && all[k] != '\n'; k--)
            {
                if (!char.IsWhiteSpace(all[k])) return false;
            }
            for (var k = t.End; k < all.Length && all[k] != '\n'; k++)
            {
                if (!char.IsWhiteSpace(all[k])) return false;
            }
            return true;
        }

        /// <summary>INTO targets of a query and RETURNING ... INTO of DML: PL/SQL only, removed from the copy.</summary>
        private void FindRemovals(int from, int to)
        {
            var s = _window;
            var kind = s.Keyword(from);
            var depth = 0;
            for (var k = from; k < to; k++)
            {
                if (s.IsSymbol(k, "(")) depth++;
                else if (s.IsSymbol(k, ")")) depth--;
                if (depth != 0) continue;

                if (kind is "SELECT" or "WITH" && s.IsKeyword(k, "INTO"))
                {
                    var start = k >= from + 2 && s.IsKeyword(k - 2, "BULK") && s.IsKeyword(k - 1, "COLLECT") ? k - 2 : k;
                    var f = k + 1;
                    var d = 0;
                    for (; f < to; f++)
                    {
                        if (s.IsSymbol(f, "(")) d++;
                        else if (s.IsSymbol(f, ")")) d--;
                        else if (d == 0 && s.IsKeyword(f, "FROM")) break;
                    }
                    _removed.Add((s.Tokens[start].Start, f < to ? s.Tokens[f].Start : s.Tokens[to - 1].End));
                    return;
                }
                if (kind is "INSERT" or "UPDATE" or "DELETE" or "MERGE" && s.Keyword(k) is "RETURNING" or "RETURN" && k > from)
                {
                    _removed.Add((s.Tokens[k - 1].End, s.Tokens[to - 1].End));
                    return;
                }
            }
        }

        /// <summary>INSERT INTO t (a, b): column names, never PL/SQL names, even when a constant has the same name.</summary>
        private void FindInsertColumnLists(int from, int to)
        {
            var s = _window;
            for (var k = from; k < to; k++)
            {
                var multiTableInto = s.IsKeyword(k, "INTO") && s.IsKeyword(from, "INSERT") && k > from + 1;
                if (!s.IsKeyword(k, "INSERT") && !multiTableInto) continue;
                var j = multiTableInto ? k : k + 1;
                if (s.IsKeyword(j, "INTO"))
                {
                    j++;
                    while (s.IsName(j) && s.IsSymbol(j + 1, ".")) j += 2;
                    j++;
                    if (s.IsName(j) && s.Keyword(j) is not ("VALUES" or "SELECT" or "WITH")) j++; // alias
                }
                if (!s.IsSymbol(j, "(") || s.Keyword(j + 1) is "SELECT" or "WITH") continue;
                var close = j;
                for (var depth = 0; close < to; close++)
                {
                    if (s.IsSymbol(close, "(")) depth++;
                    else if (s.IsSymbol(close, ")") && --depth == 0) break;
                }
                _columnLists.Add((s.Tokens[j].Start, close < to ? s.Tokens[close].End : s.Tokens[to - 1].End));
            }
        }

        private bool IsRemoved(int offset) => _removed.Exists(r => r.Start <= offset && offset < r.End);

        private static bool IsSetTarget(PlsqlSource src, int k) =>
            (src.IsKeyword(k - 1, "SET") || src.IsSymbol(k - 1, ",")) && src.IsSymbol(k + 1, "=");

        /// <summary>
        /// A column, not a PL/SQL name: an INSERT column list, or a name assigned in UPDATE ... SET (a column of the
        /// updated table, so SQL resolves it as that column everywhere in the statement).
        /// </summary>
        private bool IsColumn(PlsqlSource src, int k) =>
            (!src.IsSymbol(k + 1, ".") && _columns.Contains(src.Name(k)))
            || _columnLists.Exists(r => r.Start <= src.Tokens[k].Start && src.Tokens[k].Start < r.End)
            || _tableNames.Exists(r => r.Start <= src.Tokens[k].Start && src.Tokens[k].Start < r.End);

        /// <summary>
        /// Adds edits for the names in tokens [from, to) of <paramref name="src"/>. In a statement unknown names stay,
        /// variables become binds; in a constant's value (statement = false) a variable makes the value unusable.
        /// </summary>
        private bool Substitute(PlsqlSource src, int from, int to, bool statement, int depth, HashSet<Declaration> visiting, List<Edit> edits)
        {
            for (var next = from; next < to;)
            {
                var k = next++;
                if (!src.IsName(k) || src.IsSymbol(k - 1, ".") || src.IsSymbol(k - 1, ":") || src.IsSymbol(k - 1, "%")
                    || src.IsSymbol(k + 1, "=>") || IsTypedLiteral(src, k) || (statement && (src.IsKeyword(k - 1, "AS") || IsRemoved(src.Tokens[k].Start) || IsColumn(src, k))))
                {
                    continue;
                }
                var parts = new List<int> { k };
                while (parts.Count < 3 && src.IsSymbol(parts[^1] + 1, ".") && src.IsName(parts[^1] + 2)) parts.Add(parts[^1] + 2);
                var last = parts[^1];
                var start = src.Tokens[k].Start;
                var end = src.Tokens[last].End;
                next = last + 1;
                if (src.IsSymbol(last + 1, ".") || last >= to) continue;

                var written = src.Text[start..end];
                var called = src.IsSymbol(last + 1, "(");
                if (called && parts.Count > 1) continue; // pkg.f(x), rec.tab(i): a call or an element, never a constant: no lookup
                var found = Lookup(src, parts, statement);
                if (found.Kind == RefKind.Constant && !called && Value(found.Source!, found.Constant!, depth, visiting) is { } value)
                {
                    edits.Add(new Edit(start, end, statement ? $"{value} /*{written.Replace("*/", "* /", StringComparison.Ordinal)}*/" : value));
                    if (statement) _constants++;
                    continue;
                }
                if (found.Kind == RefKind.Unknown || (called && found.Kind == RefKind.UnknownConstant)) continue; // pkg.f(x): a function
                if (!statement) return false;
                if (called)
                {
                    Unresolved(written); // a collection element l_tab(i), a constant called like a function: no bind for it
                    continue;
                }
                var bind = found.Kind == RefKind.Variable ? Joined(src, parts) : BindName(src, parts, written);
                _bindNames.TryAdd(bind, written);
                edits.Add(new Edit(start, end, ":" + bind));
                _binds++;
                if (found.Kind != RefKind.Variable && !_constantBinds.Contains(written, StringComparer.OrdinalIgnoreCase)) _constantBinds.Add(written);
            }
            return true;
        }

        /// <summary>DATE '...', TIMESTAMP '...', INTERVAL '...': the word is part of a literal, never a name.</summary>
        private static bool IsTypedLiteral(PlsqlSource src, int k) =>
            k + 1 < src.Tokens.Count && src.Tokens[k + 1].Kind == TokenKind.StringLiteral;

        private enum RefKind { Unknown, Constant, Variable, UnknownConstant }

        private readonly record struct Ref(RefKind Kind, PlsqlSource? Source = null, Declaration? Constant = null);

        /// <summary>
        /// What a name chain refers to: a constant (with the source it is declared in); a variable or parameter
        /// (also a record in <c>rec.field</c> and a package variable); a constant of a package whose specification is
        /// not available (<c>pkg.c_x</c> that is not a table or an alias of the statement); or nothing known
        /// (a column, a function, a keyword).
        /// </summary>
        private Ref Lookup(PlsqlSource src, List<int> parts, bool statement)
        {
            var offset = src.Tokens[parts[0]].Start;
            var first = src.Name(parts[0]);
            if (parts.Count == 1)
            {
                if (InScope(src, first, offset) is { } d) return d.HasValue ? new Ref(RefKind.Constant, src, d) : new Ref(RefKind.Variable);
                return src.BodyAt(offset) is { } body ? Member(src, null, body, first, guess: false) : default;
            }
            if (parts.Count == 2)
            {
                if (InScope(src, first, offset) is { } d) return d.IsConstant ? default : new Ref(RefKind.Variable);
                if (statement && _tables.Contains(first)) return default;
                return Member(src, null, first, src.Name(parts[1]), guess: statement);
            }
            if (InScope(src, first, offset) is { } r) return r.IsConstant ? default : new Ref(RefKind.Variable); // rec.sub.field
            if (statement && _tables.Contains(src.Name(parts[1]))) return default; // schema.table.column
            return Member(src, first, src.Name(parts[1]), src.Name(parts[2]), guess: statement);
        }

        /// <summary>
        /// <c>package.name</c> in the package specification. Without the specification (neither in the text nor in the
        /// database) a name in the statement is taken for a constant when <paramref name="guess"/> is set.
        /// </summary>
        private Ref Member(PlsqlSource src, string? owner, string package, string name, bool guess)
        {
            if (name is "NEXTVAL" or "CURRVAL") return default; // a sequence
            var spec = SpecOf(src, owner, package);
            if (spec is null) return guess ? new Ref(RefKind.UnknownConstant) : default;
            // From the database the package may come under another name (a synonym): any public item of it.
            var inText = spec.Specs.Contains(package);
            var d = spec.Declarations.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal)
                && (inText ? string.Equals(x.Package, package, StringComparison.Ordinal) : x.Package is not null));
            if (d is null) return default; // a function, a type, ...
            return d.HasValue ? new Ref(RefKind.Constant, spec, d) : new Ref(RefKind.Variable);
        }

        /// <summary>The innermost declaration of <paramref name="name"/> visible at <paramref name="offset"/>.</summary>
        private static Declaration? InScope(PlsqlSource src, string name, int offset)
        {
            Declaration? best = null;
            foreach (var d in src.Declarations)
            {
                if (d.ScopeStart <= offset && offset < d.ScopeEnd && string.Equals(d.Name, name, StringComparison.Ordinal)
                    && (best is null || d.ScopeStart > best.ScopeStart))
                {
                    best = d;
                }
            }
            return best;
        }

        /// <summary>
        /// The specification of <paramref name="package"/>: in the text being resolved, in the editor, else from
        /// <see cref="ISpecSource"/> (the database), or null.
        /// </summary>
        private PlsqlSource? SpecOf(PlsqlSource src, string? owner, string package)
        {
            if (src.Specs.Contains(package)) return src;
            if (_window.Specs.Contains(package)) return _window;
            var key = $"{owner}.{package}";
            if (!_specs.TryGetValue(key, out var spec))
            {
                spec = specs is not null && _specs.Count < MaxSpecLookups && specs.PackageSpec(owner, package) is { Length: > 0 } t ? new PlsqlSource(t) : null;
                _specs[key] = spec;
            }
            return spec;
        }

        /// <summary>A bind name from the name parts: rec.field -> rec_field.</summary>
        private static string Joined(PlsqlSource src, List<int> parts) =>
            string.Join("_", parts.Select(p => src.Text.Substring(src.Tokens[p].Start, src.Tokens[p].Length).Trim('"')));

        /// <summary>A constant as a bind is named like the constant (pkg.c_x -> c_x); qualified only when two differ.</summary>
        private string BindName(PlsqlSource src, List<int> parts, string written)
        {
            var name = src.Text.Substring(src.Tokens[parts[^1]].Start, src.Tokens[parts[^1]].Length).Trim('"');
            // A variable of the same name may be bound later in the statement: it keeps the plain name.
            var variable = InScope(src, src.Name(parts[^1]), src.Tokens[parts[0]].Start) is { IsConstant: false, HasValue: false };
            if (variable || (_bindNames.TryGetValue(name, out var taken) && !string.Equals(taken, written, StringComparison.OrdinalIgnoreCase)))
            {
                name = Joined(src, parts);
            }
            _bindNames.TryAdd(name, written);
            return name;
        }

        /// <summary>The whole statement around a selection (to know its tables and aliases); the range itself otherwise.</summary>
        private (int From, int To) EnclosingStatement(int from, int to)
        {
            foreach (var (f, t, _) in Statements())
            {
                if (f <= from && to <= t) return (f, t);
            }
            return (from, to);
        }

        /// <summary>
        /// Table names and aliases of the statement (FROM, JOIN, UPDATE, INTO, USING): <c>alias.name</c> is a column,
        /// never a package constant.
        /// </summary>
        private void FindTables(int from, int to)
        {
            var s = _window;
            var inFrom = new HashSet<int>(); // bracket depths whose FROM list is open: there ',' starts the next table
            var depth = 0;
            for (var k = from; k < to; k++)
            {
                if (s.IsSymbol(k, "(")) depth++;
                else if (s.IsSymbol(k, ")")) inFrom.Remove(depth--);

                switch (s.Keyword(k))
                {
                    case "FROM":
                        inFrom.Add(depth);
                        TableAt(k + 1, to);
                        break;
                    case "JOIN" or "APPLY":
                        TableAt(k + 1, to);
                        break;
                    case "DELETE" when !s.IsKeyword(k + 1, "FROM"): // DELETE t a WHERE ...
                        TableAt(k + 1, to);
                        break;
                    case "PIVOT" or "UNPIVOT":
                        PivotAlias(k, to);
                        break;
                    case "UPDATE" or "INTO" or "USING":
                        inFrom.Remove(depth);
                        TableAt(k + 1, to);
                        break;
                    case "WHERE" or "GROUP" or "ORDER" or "CONNECT" or "START" or "HAVING" or "UNION" or "MINUS" or "INTERSECT"
                        or "FETCH" or "OFFSET" or "MODEL" or "SET" or "VALUES" or "RETURNING" or "RETURN" or "LOG" or "WHEN" or "FOR":
                        inFrom.Remove(depth);
                        break;
                    default:
                        if (s.IsSymbol(k, ",") && inFrom.Contains(depth)) TableAt(k + 1, to);
                        break;
                }
            }
        }

        /// <summary>PIVOT [XML] (...) alias / UNPIVOT [INCLUDE|EXCLUDE NULLS] (...) alias.</summary>
        private void PivotAlias(int k, int to)
        {
            var s = _window;
            var j = k + 1;
            while (j < to && !s.IsSymbol(j, "(") && j < k + 4) j++;
            if (!s.IsSymbol(j, "(")) return;
            for (var depth = 0; j < to; j++)
            {
                if (s.IsSymbol(j, "(")) depth++;
                else if (s.IsSymbol(j, ")") && --depth == 0) break;
            }
            j++;
            if (s.IsKeyword(j, "AS")) j++;
            if (j < to && s.IsName(j) && !IsClauseWord(s.Keyword(j))) _tables.Add(s.Name(j));
        }

        /// <summary>[schema.]table[@link] [AS] [alias] or (subquery) [AS] alias starting at token <paramref name="j"/>.</summary>
        private void TableAt(int j, int to)
        {
            var s = _window;
            if (s.Keyword(j) is "TABLE" or "LATERAL" or "ONLY" && s.IsSymbol(j + 1, "(")) j++;
            if (s.IsSymbol(j, "("))
            {
                for (var depth = 0; j < to; j++)
                {
                    if (s.IsSymbol(j, "(")) depth++;
                    else if (s.IsSymbol(j, ")") && --depth == 0) break;
                }
            }
            else if (s.IsName(j) && !IsClauseWord(s.Keyword(j)))
            {
                var nameStart = s.Tokens[j].Start;
                while (s.IsSymbol(j + 1, ".") && s.IsName(j + 2)) j += 2;
                _tables.Add(s.Name(j));
                if (s.IsSymbol(j + 1, "@") && s.IsName(j + 2)) j += 2;
                while (s.IsSymbol(j + 1, ".") && s.IsName(j + 2)) j += 2; // @link.domain
                _tableNames.Add((nameStart, s.Tokens[j].End));
                // A table function - XMLTABLE(...), JSON_TABLE(...), pkg.rows(...) - is followed by its alias.
                if (s.IsSymbol(j + 1, "("))
                {
                    for (var depth = 0; ++j < to;)
                    {
                        if (s.IsSymbol(j, "(")) depth++;
                        else if (s.IsSymbol(j, ")") && --depth == 0) break;
                    }
                }
            }
            else
            {
                return;
            }
            j++;
            if (s.IsKeyword(j, "AS")) j++;
            if (j < to && s.IsName(j) && !IsClauseWord(s.Keyword(j))) _tables.Add(s.Name(j));
        }

        private static bool IsClauseWord(string? word) => word is "WHERE" or "JOIN" or "INNER" or "LEFT" or "RIGHT" or "FULL"
            or "CROSS" or "NATURAL" or "ON" or "USING" or "GROUP" or "ORDER" or "CONNECT" or "START" or "HAVING" or "UNION"
            or "MINUS" or "INTERSECT" or "SET" or "VALUES" or "PARTITION" or "SAMPLE" or "PIVOT" or "UNPIVOT" or "FOR" or "WITH"
            or "LATERAL" or "OUTER" or "SELECT" or "RETURNING" or "RETURN" or "LOG" or "WHEN" or "MODEL" or "FETCH" or "OFFSET"
            or "DEFAULT" or "SUBPARTITION" or "TABLE" or "ONLY";

        /// <summary>SQL text of a constant's value, or null when it cannot be used in SQL.</summary>
        private string? Value(PlsqlSource src, Declaration d, int depth, HashSet<Declaration> visiting)
        {
            if (d.IsBoolean || d.ExprTo <= d.ExprFrom || depth >= MaxDepth || !visiting.Add(d)) return null;
            try
            {
                if (d.ExprTo - d.ExprFrom == 1 && src.Keyword(d.ExprFrom) is "TRUE" or "FALSE") return null;
                var edits = new List<Edit>();
                if (!Substitute(src, d.ExprFrom, d.ExprTo, statement: false, depth + 1, visiting, edits)) return null;
                var value = Apply(src.Text, src.Tokens[d.ExprFrom].Start, src.Tokens[d.ExprTo - 1].End, edits);
                var single = d.ExprTo - d.ExprFrom == 1 && src.IsName(d.ExprFrom); // c2 := c1: c1's value is already safe
                var literal = IsLiteral(src, d.ExprFrom, d.ExprTo);
                // A package variable may be changed at run time: only a plain initial value is trusted (g_tab := t_tab() is not).
                if (!d.IsConstant && !single && !literal) return null;
                return single || literal ? value : $"({value})";
            }
            finally
            {
                visiting.Remove(d);
            }
        }

        /// <summary>'a', 10, -1.5, NULL, DATE '2026-01-01': safe to paste without brackets.</summary>
        private static bool IsLiteral(PlsqlSource s, int from, int to)
        {
            var kind = s.Tokens[from].Kind;
            return (to - from) switch
            {
                1 => kind is TokenKind.StringLiteral or TokenKind.Number || s.IsKeyword(from, "NULL"),
                2 => (s.Tokens[from + 1].Kind == TokenKind.Number && (s.IsSymbol(from, "-") || s.IsSymbol(from, "+")))
                     || (s.Tokens[from + 1].Kind == TokenKind.StringLiteral && s.Keyword(from) is "DATE" or "TIMESTAMP"),
                _ => false,
            };
        }

        private void Unresolved(string name)
        {
            if (!_unresolved.Contains(name, StringComparer.OrdinalIgnoreCase)) _unresolved.Add(name);
        }

        private string Apply(string source, int start, int end, List<Edit> edits)
        {
            var sb = new StringBuilder();
            var pos = start;
            var cuts = _removed.Where(r => r.Start >= start && r.End <= end).Select(r => new Edit(r.Start, r.End, ""));
            foreach (var e in (ReferenceEquals(source, _window.Text) ? edits.Concat(cuts) : edits).OrderBy(e => e.Start))
            {
                if (e.Start < pos) continue;
                sb.Append(source, pos, e.Start - pos).Append(e.Replacement);
                pos = e.End;
            }
            return sb.Append(source, pos, end - pos).ToString();
        }

        private string Summary()
        {
            var parts = new List<string>();
            if (_constants > 0) parts.Add(_constants == 1 ? "1 constant" : $"{_constants} constants");
            if (_binds > 0) parts.Add(_binds == 1 ? "1 bind variable" : $"{_binds} bind variables");
            var summary = parts.Count == 0 ? "no constants found" : string.Join(", ", parts);
            if (_constantBinds.Count > 0) summary += $"; constants as binds: {Listed(_constantBinds)}";
            if (_unresolved.Count > 0) summary += $"; not resolved: {Listed(_unresolved)}";
            return summary;
        }

        private static string Listed(List<string> names) =>
            string.Join(", ", names.Take(MaxListedUnresolved)) + (names.Count > MaxListedUnresolved ? ", ..." : "");
    }
}
