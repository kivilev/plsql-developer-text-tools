namespace TextTools.Core.Sql;

/// <summary>
/// A declared name: a constant (with its value expression), a variable, or a parameter. The name is visible in
/// [<see cref="ScopeStart"/>, <see cref="ScopeEnd"/>) of the text it was declared in. <see cref="Package"/> is set
/// for items declared directly in a package specification (visible as <c>package.name</c>).
/// </summary>
public sealed record Declaration(string Name, bool IsConstant, int ScopeStart, string? Package)
{
    public int ScopeEnd { get; set; } = int.MaxValue;

    /// <summary>Token index range of the value expression (constants and package variables with an initial value).</summary>
    public int ExprFrom { get; init; }
    public int ExprTo { get; init; }

    /// <summary>Its value can be put into SQL: a constant, or a package-level variable with an initial value.</summary>
    public bool HasValue => ExprTo > ExprFrom;

    /// <summary>The declared type is BOOLEAN: not usable in SQL before Oracle 23ai.</summary>
    public bool IsBoolean { get; init; }
}

/// <summary>
/// Declarations and scopes of a PL/SQL text, found without a full parser: blocks are tracked by
/// PACKAGE / PROCEDURE / FUNCTION / DECLARE / BEGIN / IF / LOOP / CASE / END, declarations are read
/// item by item in declaration sections. Anything it does not understand is skipped, never an error.
/// </summary>
public sealed class PlsqlSource
{
    private enum FrameKind { Unit, Block, If, Loop, Case }

    private sealed class Frame(FrameKind kind, int start, bool declarations)
    {
        public FrameKind Kind { get; } = kind;
        public int Start { get; } = start;
        public bool Declarations { get; set; } = declarations;
        public string? Spec { get; init; }
        public string? Body { get; init; }
        public List<Declaration> Declared { get; } = [];
    }

    private readonly List<(string Body, int Start, int End)> _bodies = [];
    private readonly Stack<Frame> _stack = new();
    private Declaration? _loopVariable;

    public string Text { get; }
    public IReadOnlyList<Token> Tokens { get; }
    private readonly List<Declaration> _declarations = [];
    private readonly HashSet<string> _specs = new(StringComparer.Ordinal);

    public IReadOnlyList<Declaration> Declarations => _declarations;

    /// <summary>Names of the package specifications in this text.</summary>
    public IReadOnlySet<string> Specs => _specs;

    public PlsqlSource(string text)
    {
        Text = text;
        Tokens = PlsqlLexer.Tokenize(text);
        Parse();
    }

    /// <summary>Normalized name of a Word or QuotedName token: unquoted names are case-insensitive.</summary>
    public string Name(int index)
    {
        var t = Tokens[index];
        if (t.Kind == TokenKind.QuotedName)
        {
            return t.Length >= 2 && Text[t.End - 1] == '"' ? Text.Substring(t.Start + 1, t.Length - 2) : Text.Substring(t.Start + 1, t.Length - 1);
        }
        return Text.Substring(t.Start, t.Length).ToUpperInvariant();
    }

    public bool IsName(int index) => index >= 0 && index < Tokens.Count && Tokens[index].Kind is TokenKind.Word or TokenKind.QuotedName;

    /// <summary>Upper-case text of a Word token, or null for anything else (and out of range).</summary>
    public string? Keyword(int index) =>
        index >= 0 && index < Tokens.Count && Tokens[index].Kind == TokenKind.Word
            ? Text.Substring(Tokens[index].Start, Tokens[index].Length).ToUpperInvariant()
            : null;

    public bool IsKeyword(int index, string keyword) => string.Equals(Keyword(index), keyword, StringComparison.Ordinal);

    public bool IsSymbol(int index, string symbol) =>
        index >= 0 && index < Tokens.Count && Tokens[index].Kind == TokenKind.Symbol &&
        Tokens[index].Length == symbol.Length && string.CompareOrdinal(Text, Tokens[index].Start, symbol, 0, symbol.Length) == 0;

    public string Slice(int from, int to) => from >= to ? "" : Text[Tokens[from].Start..Tokens[to - 1].End];

    /// <summary>The package body that contains <paramref name="offset"/>, if any (its specification is visible there).</summary>
    public string? BodyAt(int offset)
    {
        string? found = null;
        var start = -1;
        foreach (var (body, s, e) in _bodies)
        {
            if (s <= offset && offset < e && s > start) (found, start) = (body, s);
        }
        return found;
    }

    private void Parse()
    {
        var i = 0;
        while (i < Tokens.Count)
        {
            i = _stack.TryPeek(out var top) && top.Declarations ? DeclarationItem(i, top) : Statement(i);
        }
        while (_stack.Count > 0) Close(Text.Length);
    }

    /// <summary>One item of a declaration section starting at token <paramref name="i"/>; returns the next index.</summary>
    private int DeclarationItem(int i, Frame top)
    {
        switch (Keyword(i))
        {
            case "PROCEDURE" or "FUNCTION":
                return Subprogram(i);
            case "BEGIN":
                top.Declarations = false;
                return i + 1;
            case "END":
                Close(Tokens[i].Start);
                return SkipItem(i);
            case "CURSOR":
                return Cursor(i);
            case "TYPE" or "SUBTYPE" or "PRAGMA":
                return SkipItem(i);
        }
        if (!IsName(i)) return i + 1;

        var end = SkipItem(i);
        var package = top.Spec is not null && top.Kind == FrameKind.Unit ? top.Spec : null;
        var constant = IsKeyword(i + 1, "CONSTANT");
        // A package-level variable with an initial value (c_x number := 1; without CONSTANT) is used like a constant:
        // that is how such "constants" are written in many code bases. Local variables stay variables (binds).
        var packageLevel = top.Spec is not null || top.Body is not null;
        var assign = -1;
        if (constant || packageLevel)
        {
            var depth = 0;
            for (var k = i + 1; k < end - 1 && assign < 0; k++)
            {
                if (IsSymbol(k, "(")) depth++;
                else if (IsSymbol(k, ")")) depth--;
                else if (depth == 0 && (IsSymbol(k, ":=") || IsKeyword(k, "DEFAULT"))) assign = k;
            }
        }
        if (assign > 0)
        {
            Declare(top, new Declaration(Name(i), constant, Tokens[i].Start, package)
            {
                ExprFrom = assign + 1,
                ExprTo = IsSymbol(end - 1, ";") ? end - 1 : end,
                IsBoolean = Enumerable.Range(i + 1, assign - i - 1).Any(k => IsKeyword(k, "BOOLEAN")),
            });
            return end;
        }
        Declare(top, new Declaration(Name(i), false, Tokens[i].Start, package));
        return end;
    }

    /// <summary>CURSOR name(params) ... ;: the parameters are variables visible in the cursor's query only.</summary>
    private int Cursor(int i)
    {
        var end = SkipItem(i);
        if (!IsName(i + 1) || !IsSymbol(i + 2, "(")) return end;
        var depth = 0;
        for (var j = i + 2; j < end; j++)
        {
            if (IsSymbol(j, "(")) depth++;
            else if (IsSymbol(j, ")") && --depth == 0) break;
            if (depth == 1 && (IsSymbol(j, "(") || IsSymbol(j, ",")) && IsName(j + 1))
            {
                _declarations.Add(new Declaration(Name(j + 1), false, Tokens[j + 1].Start, null) { ScopeEnd = Tokens[end - 1].End });
            }
        }
        return end;
    }

    /// <summary>A token in a block body (or outside any block) at <paramref name="i"/>; returns the next index.</summary>
    private int Statement(int i)
    {
        var start = Tokens[i].Start;
        switch (Keyword(i))
        {
            case "PACKAGE":
                return Package(i);
            case "PROCEDURE" or "FUNCTION":
                return Subprogram(i);
            case "DECLARE":
                _stack.Push(new Frame(FrameKind.Unit, start, declarations: true));
                break;
            case "BEGIN":
                _stack.Push(new Frame(FrameKind.Block, start, declarations: false));
                break;
            case "IF":
                _stack.Push(new Frame(FrameKind.If, start, declarations: false));
                break;
            case "FOR" when IsName(i + 1) && IsKeyword(i + 2, "IN"):
                // FOR rec IN ... LOOP: the loop record (or index) is a variable of the loop.
                _loopVariable = new Declaration(Name(i + 1), false, Tokens[i + 1].Start, null);
                return i + 2;
            case "LOOP":
                var loop = new Frame(FrameKind.Loop, start, declarations: false);
                _stack.Push(loop);
                if (_loopVariable is { } v) Declare(loop, v);
                _loopVariable = null;
                break;
            case "CASE":
                _stack.Push(new Frame(FrameKind.Case, start, declarations: false));
                break;
            case "END":
                return End(i);
        }
        return i + 1;
    }

    private int End(int i)
    {
        var kind = Keyword(i + 1) switch
        {
            "IF" => FrameKind.If,
            "LOOP" => FrameKind.Loop,
            "CASE" => FrameKind.Case,
            _ => (FrameKind?)null,
        };
        if (kind is { } k)
        {
            // END IF / END LOOP / END CASE: close up to the matching frame.
            while (_stack.TryPeek(out var f) && f.Kind != k && f.Kind is not (FrameKind.Unit or FrameKind.Block)) Close(Tokens[i].Start);
            if (_stack.TryPeek(out var m) && m.Kind == k) Close(Tokens[i].Start);
            return i + 2;
        }
        if (_stack.TryPeek(out var top) && top.Kind == FrameKind.Case)
        {
            _stack.Pop(); // END of a CASE expression
            return i + 1;
        }
        while (_stack.TryPeek(out var f) && f.Kind is not (FrameKind.Unit or FrameKind.Block)) Close(Tokens[i].Start);
        Close(Tokens[i].Start);
        // END name; - the name is a label, not the next declaration of the enclosing section.
        return IsName(i + 1) && IsSymbol(i + 2, ";") ? i + 3 : i + 1;
    }

    /// <summary>PACKAGE [BODY] [schema.]name ... IS|AS: a unit whose top-level declarations are public (spec) or see the spec (body).</summary>
    private int Package(int i)
    {
        var j = i + 1;
        var body = IsKeyword(j, "BODY");
        if (body) j++;
        if (!IsName(j)) return i + 1;
        if (IsSymbol(j + 1, ".") && IsName(j + 2)) j += 2;
        var name = Name(j);
        var isAt = FindIsAs(j + 1);
        if (isAt < 0) return j + 1;
        if (!body) _specs.Add(name);
        _stack.Push(new Frame(FrameKind.Unit, Tokens[i].Start, declarations: true) { Spec = body ? null : name, Body = body ? name : null });
        return isAt + 1;
    }

    /// <summary>
    /// PROCEDURE|FUNCTION name [(params)] ... IS|AS opens a unit with the parameters declared in it.
    /// A declaration ending with ';' (spec, forward declaration) or an external one (LANGUAGE ...) opens nothing.
    /// </summary>
    private int Subprogram(int i)
    {
        var j = i + 1;
        if (!IsName(j)) return i + 1;
        if (IsSymbol(j + 1, ".") && IsName(j + 2)) j += 2;
        j++;

        var parameters = new List<Declaration>();
        if (IsSymbol(j, "("))
        {
            var depth = 0;
            for (; j < Tokens.Count; j++)
            {
                if (IsSymbol(j, "(")) depth++;
                else if (IsSymbol(j, ")") && --depth == 0) break;
                if (depth == 1 && (IsSymbol(j, "(") || IsSymbol(j, ",")) && IsName(j + 1))
                {
                    parameters.Add(new Declaration(Name(j + 1), false, Tokens[j + 1].Start, null));
                }
            }
            j++;
        }
        var isAt = FindIsAs(j);
        if (isAt < 0) return SkipItem(i);
        if (Keyword(isAt + 1) is "LANGUAGE" or "EXTERNAL") return SkipItem(isAt);

        var frame = new Frame(FrameKind.Unit, Tokens[i].Start, declarations: true);
        _stack.Push(frame);
        foreach (var p in parameters) Declare(frame, p);
        return isAt + 1;
    }

    /// <summary>Index of IS/AS at bracket depth 0 before ';', or -1 (also when a ')' closes an enclosing bracket).</summary>
    private int FindIsAs(int j)
    {
        var depth = 0;
        for (; j < Tokens.Count; j++)
        {
            if (IsSymbol(j, "(")) depth++;
            else if (IsSymbol(j, ")") && --depth < 0) return -1;
            else if (depth == 0 && IsSymbol(j, ";")) return -1;
            else if (depth == 0 && Keyword(j) is "IS" or "AS") return j;
        }
        return -1;
    }

    /// <summary>Index after the ';' that ends the item at <paramref name="i"/> (bracket depth 0), or the end.</summary>
    private int SkipItem(int i)
    {
        var depth = 0;
        for (var j = i; j < Tokens.Count; j++)
        {
            if (IsSymbol(j, "(")) depth++;
            else if (IsSymbol(j, ")")) depth--;
            else if (depth <= 0 && IsSymbol(j, ";")) return j + 1;
        }
        return Tokens.Count;
    }

    private void Declare(Frame frame, Declaration d)
    {
        frame.Declared.Add(d);
        _declarations.Add(d);
    }

    private void Close(int end)
    {
        if (!_stack.TryPop(out var frame)) return;
        foreach (var d in frame.Declared) d.ScopeEnd = end;
        if (frame.Body is { } body) _bodies.Add((body, frame.Start, end));
    }
}
