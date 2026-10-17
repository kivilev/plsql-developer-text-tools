# Code Review: "SQL Tools -> Copy SQL with constants resolved"

## Executive Summary

The feature **"SQL Tools -> Copy SQL with constants resolved"** was reviewed across its core logic (`src/TextTools.Core/Sql/`) and native plugin integration (`src/TextTools.Plugin/`).

- **Test Suite Results**: 235 tests executed (`dotnet test -c Release`), 221 passed, 14 failed in `AgyFoundBugs` reproducing verified defects.
- **Compiler / Analyzer Health**: Clean build, 0 warnings, 0 errors under .NET 10 Release mode with strict analyzers.
- **Safety Guarantee**: The invariant that TextTools.Core never throws exceptions other than `FormatException` holds across hostile and malformed inputs.
- **Key Findings**: 14 distinct correctness bugs were identified and reproduced with minimal tests in [AgyReviewTests.cs](file:///tests/TextTools.Core.Tests/AgyReviewTests.cs). The most critical defects involve:
  1. Table aliases in `TABLE()` and `LATERAL()` being misidentified as unknown package constants, converting table columns to bind variables.
  2. `END <subprogram_name>;` leaking procedure and function names as declared package variables, corrupting subsequent SQL function calls.
  3. Multi-column `UPDATE SET (col1, col2) = ...` overwriting update column targets with constant values.
  4. Semicolon-truncated `WITH FUNCTION` queries.

---

## 1. List of Found Bugs

### Bug 1: `TABLE()` collection expression alias mistaken for package constant
- **Severity**: **Critical**
- **File**: [ConstantResolver.cs](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L453-L457) (`IsClauseWord`, `FindTables`, `TableAt`)
- **Minimal Repro**:
  ```sql
  select t.id from table(pkg.get_rows()) t§
  ```
- **Expected**: `select t.id from table(pkg.get_rows()) t` (`t.id` is a column of table alias `t`).
- **Actual**: `select :id from table(pkg.get_rows()) t` (`t.id` is converted to a bind variable `:id`).
- **Root Cause**: `IsClauseWord` contains `"TABLE"`. In `TableAt(j, to)`, when evaluating `FROM table(...) t`, token `j` is `"TABLE"`. `IsClauseWord` returns `true`, causing `TableAt` to return immediately without skipping the parentheses or reading the alias `t`. Consequently, `t` is never added to `_tables`. Later, `Lookup` treats `t` as a package name and `id` as an unknown package constant.
- **Suggested Fix**: In `TableAt`, detect when `s.IsKeyword(j, "TABLE") && s.IsSymbol(j + 1, "(")`. Advance `j` past the balanced parentheses of `TABLE(...)`, then read the optional `AS` and the alias identifier, adding it to `_tables`.

---

### Bug 2: `LATERAL` inline view alias mistaken for package constant
- **Severity**: **Critical**
- **File**: [ConstantResolver.cs](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L453-L457) (`IsClauseWord`, `TableAt`)
- **Minimal Repro**:
  ```sql
  select lat.val from orders o, lateral(select 1 val from dual) lat§
  ```
- **Expected**: `select lat.val from orders o, lateral(select 1 val from dual) lat`.
- **Actual**: `select :val from orders o, lateral(select 1 val from dual) lat`.
- **Root Cause**: `"LATERAL"` is listed in `IsClauseWord`. In `TableAt`, encountering `LATERAL` aborts table parsing immediately, failing to record `lat` in `_tables`.
- **Suggested Fix**: In `TableAt`, if `s.IsKeyword(j, "LATERAL") && s.IsSymbol(j + 1, "(")`, skip past the parenthesized subquery and record the trailing alias `lat`.

---

### Bug 3: `PIVOT` table alias mistaken for package constant
- **Severity**: **Critical**
- **File**: [ConstantResolver.cs](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L412-L420) (`FindTables`)
- **Minimal Repro**:
  ```sql
  select pvt.q1 from orders pivot (sum(amount) for qtr in (1 as q1)) pvt§
  ```
- **Expected**: `select pvt.q1 from orders pivot (sum(amount) for qtr in (1 as q1)) pvt`.
- **Actual**: `select :q1 from orders pivot (sum(amount) for qtr in (1 as q1)) pvt`.
- **Root Cause**: `FindTables` treats `PIVOT` and `UNPIVOT` as terminating the `FROM` list (like `WHERE` or `GROUP BY`), but does not parse the `PIVOT (...) alias` clause. `pvt` is omitted from `_tables`.
- **Suggested Fix**: In `FindTables`, handle `PIVOT` / `UNPIVOT` by skipping the parenthesized pivot specification and registering the subsequent alias into `_tables`.

---

### Bug 4: Multi-column `UPDATE SET (a, b) = ...` target columns overwritten by constants
- **Severity**: **Critical**
- **File**: [ConstantResolver.cs](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L224-L235) (`IsSetTarget`, `_columns`)
- **Minimal Repro**:
  ```sql
  declare
    a constant number := 10;
  begin
    update t set (a, b) = (select 1, 2 from dual)§;
  end;
  ```
- **Expected**: `update t set (a, b) = (select 1, 2 from dual)`.
- **Actual**: `update t set (10 /*a*/, b) = (select 1, 2 from dual)` (corrupts SQL syntax).
- **Root Cause**: `IsSetTarget` checks only single-column assignments:
  `(src.IsKeyword(k - 1, "SET") || src.IsSymbol(k - 1, ",")) && src.IsSymbol(k + 1, "=")`.
  In `SET (a, b) =`, `a` is preceded by `(` and followed by `,`. It is not added to `_columns`, so `Substitute` replaces it with the constant value.
- **Suggested Fix**: When encountering `SET (` in an `UPDATE` statement, add all identifiers in the comma-delimited list before `)` and `=` into `_columns`.

---

### Bug 5: `END <subprogram_name>;` leaks procedure/function name as a package variable
- **Severity**: **Critical**
- **File**: [PlsqlSource.cs](file:///src/TextTools.Core/Sql/PlsqlSource.cs#L214-L216) (`End`)
- **Minimal Repro**:
  ```sql
  create or replace package body pkg is
    procedure helper is
    begin
      null;
    end helper;

    procedure run is
    begin
      select helper from dual§;
    end run;
  end pkg;
  ```
- **Expected**: `select helper from dual` (`helper` is a subprogram, not a variable).
- **Actual**: `select :helper from dual`.
- **Root Cause**: In `PlsqlSource.End(i)`, when closing a subprogram, it returns `i + 1` (the subprogram name `helper`). Because the parent `PACKAGE BODY` frame is in `Declarations = true` mode, `DeclarationItem` parses `helper;` as a variable declaration and registers it in `_declarations`. Any subsequent reference in SQL is then treated as a variable and turned into a bind `:helper`.
- **Suggested Fix**: In `End(i)`, if `Keyword(i + 1)` is not `IF`/`LOOP`/`CASE`, check if `IsName(i + 1)`: advance past the trailing subprogram/block name so `DeclarationItem` never sees it as a declaration.

---

### Bug 6: Cursor parameters left as unbound identifiers in cursor query
- **Severity**: **Major**
- **File**: [PlsqlSource.cs](file:///src/TextTools.Core/Sql/PlsqlSource.cs#L122-L123) (`DeclarationItem`)
- **Minimal Repro**:
  ```sql
  declare
    cursor cur(p_id in number) is
      select * from orders where order_id = p_id§;
  begin
    null;
  end;
  ```
- **Expected**: `select * from orders where order_id = :p_id`.
- **Actual**: `select * from orders where order_id = p_id`.
- **Root Cause**: `case "CURSOR" or "TYPE" or "SUBTYPE" or "PRAGMA": return SkipItem(i);`
  `CURSOR` declarations are skipped without extracting their parameters. `ConstantResolver` cannot find `p_id` in scope and leaves it as an unresolvable column identifier.
- **Suggested Fix**: In `DeclarationItem`, parse `CURSOR <name> [(params)] IS`: register `params` into a frame visible within the cursor's query up to the terminating `;`.

---

### Bug 7: Bind name collision between local variable and package constant
- **Severity**: **Major**
- **File**: [ConstantResolver.cs](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L274) (`Substitute`, `Joined`, `BindName`)
- **Minimal Repro**:
  ```sql
  procedure p(c_id in number) is
  begin
    select c_id, pkg.c_id from dual§;
  end;
  ```
- **Expected**: `select :c_id, :pkg_c_id from dual` (the package constant should disambiguate to avoid variable collision).
- **Actual**: `select :c_id, :c_id from dual`.
- **Root Cause**: `Joined(src, parts)` formats variable binds but does not register them in `_bindNames`. When `BindName` later checks for collisions for `pkg.c_id`, `_bindNames` has no record of `:c_id`, so both receive the same bind name.
- **Suggested Fix**: In `Substitute`, register all variable binds into `_bindNames` (`_bindNames[varName] = written`), ensuring `BindName` detects the collision and qualifies the constant.

---

### Bug 8: Oracle 12c+ `WITH FUNCTION` clause truncated by semicolon
- **Severity**: **Major**
- **File**: [ConstantResolver.cs](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L114-L120) (`Statements`)
- **Minimal Repro**:
  ```sql
  with function get_val return number is
  begin
    return 42;
  end;
  select get_val from dual§;
  ```
- **Expected**: `with function get_val return number is begin return 42; end; select get_val from dual`.
- **Actual**: `select get_val from dual` (the `WITH FUNCTION` declaration is severed and lost).
- **Root Cause**: `Statements()` scans forward and terminates on `s.IsSymbol(j, ";")` whenever bracket depth is 0. In PL/SQL declarations inside `WITH`, the semicolons after statements inside `BEGIN ... END;` occur at bracket depth 0, terminating statement location prematurely.
- **Suggested Fix**: In `Statements()`, when a `WITH` clause contains `FUNCTION` or `PROCEDURE`, track `BEGIN ... END` block depth in addition to bracket depth before treating `;` as the statement boundary.

---

### Bug 9: Parenthesized `SELECT` expressions rejected by `StartsStatement`
- **Severity**: **Major**
- **File**: [ConstantResolver.cs](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L134-L136) (`StartsStatement`)
- **Minimal Repro**:
  ```sql
  begin
    return (select count(*) from orders where status = c_status§);
  end;
  ```
- **Expected**: Copies `select count(*) from orders where status = 'ACTIVE' /*c_status*/`.
- **Actual**: Throws `FormatException: put the cursor in a SQL statement (SELECT, WITH, INSERT, UPDATE, DELETE, MERGE) or select one`.
- **Root Cause**: Line 135 only allows `(` before `SELECT` when `p - 1` is `IN` (`(s.IsSymbol(p, "(") && s.IsKeyword(p - 1, "IN"))`). Parenthesized scalar subqueries in assignments (`:= (`), returns (`RETURN (`), or comparisons are rejected.
- **Suggested Fix**: Allow `SELECT` preceded by `(` when `p - 1` is an operator or keyword like `RETURN`, `:=`, `=`, etc.

---

### Bug 10: Multi-table `INSERT ALL / FIRST` column lists not protected
- **Severity**: **Major**
- **File**: [ConstantResolver.cs](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L202-L211) (`FindInsertColumnLists`)
- **Minimal Repro**:
  ```sql
  declare
    col1 constant number := 99;
  begin
    insert all
      into t1 (col1) values (1)
    select 1 from dual§;
  end;
  ```
- **Expected**: `insert all into t1 (col1) values (1) select 1 from dual`.
- **Actual**: `insert all into t1 (99 /*col1*/) values (1) select 1 from dual`.
- **Root Cause**: `FindInsertColumnLists` only looks for `INSERT INTO`. If `ALL` or `FIRST` immediately follows `INSERT`, it skips the statement, leaving target column lists unprotected against constant replacement.
- **Suggested Fix**: In `FindInsertColumnLists`, if `s.Keyword(j) is "ALL" or "FIRST"`, scan all subsequent `INTO <table_name> (<columns>)` clauses in the statement and add their column lists to `_columnLists`.

---

### Bug 11: Nested record variable fields (3-part names) mistaken for `schema.package.constant`
- **Severity**: **Major**
- **File**: [ConstantResolver.cs](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L306-L308) (`Lookup`)
- **Minimal Repro**:
  ```sql
  declare
    l_user user_rec_t;
  begin
    select * from accounts where id = l_user.contact.phone§;
  end;
  ```
- **Expected**: `select * from accounts where id = :l_user_contact_phone`.
- **Actual**: `select * from accounts where id = :phone` (and reported in summary as `constants as binds: l_user.contact.phone`).
- **Root Cause**: In `Lookup`, when `parts.Count == 3`, it immediately treats `first` as a schema/owner and `parts[1]` as a package name without checking if `first` is a local variable in scope.
- **Suggested Fix**: When `parts.Count == 3`, check `if (InScope(src, first, offset) is { IsConstant: false }) return new Ref(RefKind.Variable);` before schema/package lookup.

---

### Bug 12: Quoted identifiers with escaped quotes (`""`) split by lexer
- **Severity**: **Major**
- **File**: [PlsqlLexer.cs](file:///src/TextTools.Core/Sql/PlsqlLexer.cs#L56)
- **Minimal Repro**:
  ```sql
  declare
    "c""x" constant number := 42;
  begin
    select "c""x" from dual§;
  end;
  ```
- **Expected**: `select 42 /*"c""x"*/ from dual`.
- **Actual**: `select :c"x" from dual`.
- **Root Cause**: `PlsqlLexer` searches for the first `"` via `text.IndexOf('"', i + 1)`. In Oracle SQL/PLSQL, `""` inside a quoted identifier represents an escaped quote. The lexer splits `"c""x"` into `"c"` and `""x"`, breaking declaration and reference matching.
- **Suggested Fix**: Implement `QuotedNameEnd(string text, int i)` that skips doubled quotes `""` similarly to `PlainStringEnd` for `''`.

---

### Bug 13: Chained constant literal rendered with redundant parentheses
- **Severity**: **Minor**
- **File**: [ConstantResolver.cs](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L469) (`Value`, `IsLiteral`)
- **Minimal Repro**:
  ```sql
  declare
    c1 constant number := 10;
    c2 constant number := c1;
  begin
    select c2 from dual§;
  end;
  ```
- **Expected**: `select 10 /*c2*/ from dual`.
- **Actual**: `select (10) /*c2*/ from dual`.
- **Root Cause**: `IsLiteral` is called with the source token range of `c2`, which is `c1` (`TokenKind.Word`). It returns `false`, causing `Value` to wrap the substituted literal `10` in parentheses `(10)`.
- **Suggested Fix**: In `Value`, check whether the resolved string `value` is itself a literal (number, single-quoted string, or null) before wrapping in parentheses.

---

### Bug 14: Cursor on trailing whitespace after statement semicolon throws FormatException
- **Severity**: **Minor**
- **File**: [ConstantResolver.cs](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L88-L95) (`Range`, `Statements`)
- **Minimal Repro**:
  `"select 1 from dual; §"`
- **Expected**: Copies `select 1 from dual`.
- **Actual**: Throws `FormatException: put the cursor in a SQL statement (SELECT, WITH, INSERT, UPDATE, DELETE, MERGE) or select one`.
- **Root Cause**: `until` in `Statements()` stops strictly at `s.Tokens[j].End` (the semicolon token). If the user places the cursor after the semicolon before the end of the line, `Cursor <= until` evaluates to `false`.
- **Suggested Fix**: Extend `until` to encompass trailing spaces and tabs up to the next newline `\n`.

---

## 2. Design Risks and Architectural Recommendations

### 2.1 Lexer-Only vs Parser-Assisted Approach
- **Risk**: The current architecture relies entirely on lightweight lexical scanning (`PlsqlLexer` and token-stream lookahead in `PlsqlSource`). While this keeps the engine fast and robust against syntax errors, SQL constructs such as `TABLE(expr) alias`, `LATERAL(...) alias`, `PIVOT(...) alias`, multi-column `SET (a, b) =`, and `WITH FUNCTION ... END;` exhibit structure that breaks token-level heuristics.
- **Recommendation**: Introduce a lightweight clause scanner for SQL table sources. Instead of relying solely on `IsClauseWord`, maintain a small state machine for `FROM` / `JOIN` that handles:
  1. `TABLE(...) [alias]`
  2. `LATERAL(...) [alias]`
  3. `PIVOT / UNPIVOT (...) [alias]`
  4. `JSON_TABLE / XMLTABLE (...) [alias]`

### 2.2 SQL Injection and Security of `SpecQuery`
- **Assessment**: `SpecQuery.cs` was inspected and verified safe against SQL injection:
  - Table and package names are sanitized with `Literal(value)`, replacing `'` with `''`.
  - In Oracle SQL, single quotes are doubled; no C-style backslash escape sequences exist in standard string literals.
- **Recommendation**: For defensive depth, validate that `owner` and `package` match valid Oracle identifier syntax (`^[A-Za-z0-9_#$]+$`) before issuing dictionary queries to `ALL_SOURCE` and `ALL_SYNONYMS`.

### 2.3 Database Dictionary Query Overhead & Synonym Recursion
- **Current Behavior**: `DatabaseSpecs` queries `ALL_SOURCE` via `Ide.QueryColumn(SpecQuery.For(...))`. It caches results in `Dictionary<string, PlsqlSource?> _specs` with a hard limit of `MaxSpecLookups = 50`.
- **Risk**: When an identifier `x.y` is not in `_tables`, the resolver assumes `x` is a package and queries the database for its spec. On large queries with many unqualified columns or aliases missed by `TableAt` (e.g. `t.id` in `TABLE()` or `LATERAL`), this can trigger up to 50 synchronous dictionary queries through the IDE's connection.
- **Recommendation**:
  1. Improve table alias detection (Bugs 1, 2, 3) to prevent table aliases from triggering dictionary lookups.
  2. Cache known non-existent packages (`null` values) per session to avoid repeated dictionary misses.

### 2.4 IDE Stability and Crash Resistance
- **Verification**:
  - `PlsqlLexer` and `PlsqlSource` never throw on unclosed strings, unmatched brackets, or hostile inputs; unterminated constructs safely run to EOF.
  - Recursion in constant chains is bounded by `MaxDepth = 10` and cycle detection (`HashSet<Declaration> visiting`), completely preventing stack overflows.
  - Native interop (`Clipboard.cs`, `Ide.cs`) wraps Win32/Cdecl calls with proper allocations, frees, and try/catch handlers.

---

## 3. What Was Tested and Found Correct

The following capabilities were tested in [AgyReviewTests.cs](file:///tests/TextTools.Core.Tests/AgyReviewTests.cs) and verified to function correctly:

1. **Alternative Quote Delimiters (q-quotes)**: Handled properly with brackets `[]`, braces `{}`, angle brackets `<>`, and symbols `//`, `##`.
2. **Unicode & National Identifiers**: Cyrillic variable and constant names (`константа`) tokenize and resolve accurately with uppercase normalization.
3. **Literal Formatting**: Negative numbers (`-0.75`), `NULL` literals, `DATE 'YYYY-MM-DD'`, and `TIMESTAMP '...'` render as clean literals without redundant parentheses.
4. **Scope Precedence**:
   - Innermost declarations take priority over outer blocks and package body variables.
   - Package body initialization blocks (`BEGIN ... END;` at the package body level) correctly see package body constants.
   - Exception handlers (`EXCEPTION WHEN ...`) retain full visibility of enclosing block declarations.
   - Subprograms with keywords like `DETERMINISTIC` and `RESULT_CACHE` parse correctly.
   - Database triggers (`BEFORE INSERT OR UPDATE ... DECLARE ... BEGIN`) locate and resolve local constants.
5. **Loops & Control Flow**: `WHILE ... LOOP`, simple `LOOP`, and `CASE ... END` expressions in SELECT lists do not corrupt block tracking.
6. **DML & Query Cleanups**:
   - Multi-line `BULK COLLECT INTO` and multi-column `RETURNING a, b INTO l_a, l_b` are removed cleanly without syntax artifacts.
   - INSERT column lists in single-table `INSERT INTO t (col1, col2) VALUES (...)` protect column names while substituting constant values in `VALUES`.
   - `UPDATE ... SET col = c_val` protects target column names from replacement across the entire statement.
7. **Cross-Package Constants**: Chained constant lookups across distinct package specs retrieved via `ISpecSource` resolve recursively up to the depth limit.
8. **Cycle Protection**: Mutual cycles across package specifications (`pkg_a.c_a := pkg_b.c_b` and vice-versa) fall back gracefully to bind variables without infinite recursion or hanging.
9. **SQL Functions and Sequences**: `seq.nextval`, `seq.currval`, and multi-argument function calls (`dbms_lob.substr(...)`, `pkg.fn(...)`) remain untouched.
10. **Slash Separation**: SQL statements separated by standalone `/` lines locate the statement under the cursor accurately.
11. **Selection Mode**: Highlighting a sub-expression or subquery preserves the table aliases of the enclosing statement.
