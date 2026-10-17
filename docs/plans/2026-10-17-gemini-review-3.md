# Code Review: Resolve Constants Phase 2 (Database Lookups, Package Variables, Context Menu & CI Matrix)

Independent code review of changes since the first review, focusing on:
1. Database lookups (`src/TextTools.Plugin/DatabaseSpecs.cs`, `src/TextTools.Core/Sql/SpecQuery.cs`, `Ide.cs` callbacks 110 & `QueryColumn`, `ISpecSource` integration in `ConstantResolver.cs`).
2. Package-level variables with initial values vs constants (`PlsqlSource.cs`, `ConstantResolver.Value`, scopes, subtypes, `%TYPE`).
3. Unknown constants becoming binds and bind name collision handling.
4. Context menu registration (`Exports.OnActivate`, `Commands.ContextMenu`, idempotency).
5. Real IDE GitHub Actions matrix (`.github/workflows/real-ide.yml`, `scripts/real-ide-smoke.ps1`).

New test suite: `tests/TextTools.Core.Tests/GeminiReview3Tests.cs` (38 tests: 30 passing in `GeminiReview3Tests`, 8 failing reproducing real defects in `GeminiReview3BugTests`).

---

## 1. Bugs Found

### Bug 1: `XMLTABLE` table alias in `FROM` clause not recognized, corrupting columns into bind variables
- **Severity**: **Critical**
- **File:Line**: [ConstantResolver.cs:467](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L467) (`TableAt`)
- **Minimal Repro**:
  ```sql
  select xt.id from xmltable('/rows/row' passing xml columns id number path '@id') xt§
  ```
- **Expected**: `select xt.id from xmltable('/rows/row' passing xml columns id number path '@id') xt` (`xt.id` is a column of table alias `xt`).
- **Actual**: `select :id from xmltable('/rows/row' passing xml columns id number path '@id') xt` (`xt.id` is converted to bind variable `:id`).
- **Root Cause**: `TableAt` checks:
  `if (s.Keyword(j) is "TABLE" or "LATERAL" or "ONLY" && s.IsSymbol(j + 1, "(")) j++;`
  It does not check `"XMLTABLE"`. Encountering `xmltable(...) xt`, it treats `xmltable` as a regular table name, adds `"XMLTABLE"` to `_tables`, and halts without skipping past the parenthesized clause `(...)`. The trailing alias `xt` is never reached or added to `_tables`. When evaluating `xt.id`, `_tables.Contains("XT")` is false, treating `xt` as an unknown package and `id` as an unknown package constant.
- **Concrete Fix**: In [ConstantResolver.cs:467](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L467), add `"XMLTABLE"` and `"JSON_TABLE"`:
  ```csharp
  if (s.Keyword(j) is "TABLE" or "LATERAL" or "ONLY" or "XMLTABLE" or "JSON_TABLE" && s.IsSymbol(j + 1, "(")) j++;
  ```

---

### Bug 2: `JSON_TABLE` table alias in `FROM` clause not recognized
- **Severity**: **Critical**
- **File:Line**: [ConstantResolver.cs:467](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L467) (`TableAt`)
- **Minimal Repro**:
  ```sql
  select jt.val from json_table(doc, '$[*]' columns (val varchar2(10) path '$.val')) jt§
  ```
- **Expected**: `select jt.val from json_table(doc, '$[*]' columns (val varchar2(10) path '$.val')) jt`
- **Actual**: `select :val from json_table(doc, '$[*]' columns (val varchar2(10) path '$.val')) jt`
- **Root Cause**: Same as Bug 1. `JSON_TABLE` is omitted from the table-function check in `TableAt`, leaving alias `jt` unregistered in `_tables`. Column `jt.val` is corrupted into `:val`.
- **Concrete Fix**: Included in the fix for Bug 1 above.

---

### Bug 3: `CROSS APPLY` and `OUTER APPLY` table aliases omitted from `_tables`
- **Severity**: **Critical**
- **File:Line**: [ConstantResolver.cs:424-436](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L424-L436) (`FindTables`), [ConstantResolver.cs:494-498](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L494-L498) (`IsClauseWord`)
- **Minimal Repro**:
  ```sql
  select dt.amount from orders o cross apply (select * from items i where i.order_id = o.id) dt§
  select dt.val from orders o outer apply (select 1 val from dual) dt§
  ```
- **Expected**: Columns `dt.amount` and `dt.val` remain column references of alias `dt`.
- **Actual**: `dt.amount` becomes `:amount` and `dt.val` becomes `:val`.
- **Root Cause**: In Oracle 12c+, `CROSS APPLY` and `OUTER APPLY` specify lateral joins without the `JOIN` keyword. `FindTables` only dispatches on `case "JOIN":` and `case "FROM":`. When encountering `CROSS APPLY (...) dt`, neither keyword triggers table parsing. Alias `dt` is never added to `_tables`, causing `dt.<col>` to be mistaken for an unknown package constant.
- **Concrete Fix**: In `FindTables`, add `case "APPLY":` and add `"APPLY"` to `IsClauseWord`:
  ```csharp
  case "JOIN" or "APPLY":
      TableAt(k + 1, to);
      break;
  ```

---

### Bug 4: `DELETE <table_name> [alias]` without `FROM` omits table alias, corrupting `WHERE` clause columns
- **Severity**: **Major**
- **File:Line**: [ConstantResolver.cs:431-434](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L431-L434) (`FindTables`)
- **Minimal Repro**:
  ```sql
  delete orders o where o.id = 1§
  ```
- **Expected**: `delete orders o where o.id = 1`
- **Actual**: `delete orders o where :id = 1`
- **Root Cause**: In Oracle SQL, the `FROM` keyword in `DELETE` is optional (`DELETE [FROM] table_name [alias] ...`). `FindTables` handles `case "UPDATE" or "INTO" or "USING":` but does not handle `case "DELETE":`. Without `FROM`, neither `orders` nor `o` is registered in `_tables`. Column reference `o.id` in `WHERE` is treated as package `o` and constant `id`, replacing it with bind `:id`.
- **Concrete Fix**: In `FindTables`, include `"DELETE"` in the target clause switch:
  ```csharp
  case "UPDATE" or "INTO" or "USING" or "DELETE":
      inFrom.Remove(depth);
      TableAt(k + 1, to);
      break;
  ```

---

### Bug 5: Bind name collision when an unknown package constant appears before a PL/SQL variable of the same name
- **Severity**: **Major**
- **File:Line**: [ConstantResolver.cs:291-294](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L291-L294) (`Substitute`), [ConstantResolver.cs:384-393](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L384-L393) (`BindName`)
- **Minimal Repro**:
  ```sql
  declare
    c_val number;
  begin
    select pkg.c_val, c_val from dual§;
  end;
  ```
- **Expected**: `select :pkg_c_val, :c_val from dual` (distinct binds for distinct identifiers).
- **Actual**: `select :c_val, :c_val from dual` (both the package constant and the local variable collapse into the exact same bind variable).
- **Root Cause**: When `pkg.c_val` is parsed first, `_bindNames` is empty. `BindName` claims bind `:c_val` for `pkg.c_val`. When variable `c_val` is evaluated next, line 291 executes:
  `var bind = found.Kind == RefKind.Variable ? Joined(src, parts) : BindName(src, parts, written);`
  For variables, `Joined` is called directly without checking `_bindNames`. `Joined` returns `"c_val"`. `_bindNames.TryAdd("c_val", "c_val")` is a no-op because `"c_val"` is already present. The variable is replaced with `:c_val`, unintentionally merging two separate entities into one bind.
  *(Note: when the order is reversed as `select c_val, pkg.c_val`, the variable claims `:c_val` first, causing `pkg.c_val` to detect the collision and correctly qualify as `:pkg_c_val`. The bug only manifests when the constant precedes the variable).*
- **Concrete Fix**: In `Substitute`, when processing a variable, verify whether `Joined(src, parts)` is already occupied in `_bindNames` by a different expression (`taken != written`). Alternatively, pre-register in-scope variables in `_bindNames` before running substitutions so `BindName` immediately qualifies colliding constants.

---

### Bug 6: `SpecOf` matches unqualified editor package spec when looking up a schema-qualified package
- **Severity**: **Major**
- **File:Line**: [ConstantResolver.cs:368-369](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L368-L369) (`SpecOf`)
- **Minimal Repro**:
  Editor contains: `package pkg is c_val constant number := 1; end;`
  Database `ISpecSource` contains: `remote_owner.pkg` with `c_val := 999`.
  Query evaluated:
  ```sql
  select remote_owner.pkg.c_val from dual§
  ```
- **Expected**: `select 999 /*remote_owner.pkg.c_val*/ from dual` (fetches `remote_owner.pkg` from the database).
- **Actual**: `select 1 /*remote_owner.pkg.c_val*/ from dual` (hijacks constant value `1` from the local window's `pkg`).
- **Root Cause**: `SpecOf` checks:
  ```csharp
  if (src.Specs.Contains(package)) return src;
  if (_window.Specs.Contains(package)) return _window;
  ```
  It does not check whether `owner is null`. Even though the identifier in the query is explicitly qualified as `remote_owner.pkg`, `_window.Specs.Contains("PKG")` evaluates to `true` and returns the local package specification without consulting `ISpecSource`.
- **Concrete Fix**: Require `owner is null` when matching editor package specs:
  ```csharp
  if (owner is null && src.Specs.Contains(package)) return src;
  if (owner is null && _window.Specs.Contains(package)) return _window;
  ```

---

### Bug 7: ANSI `DATE` and `TIMESTAMP` literals corrupted when a variable named `date` or `timestamp` is in scope
- **Severity**: **Major**
- **File:Line**: [ConstantResolver.cs:261](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L261) (`Substitute`)
- **Minimal Repro**:
  ```sql
  declare
    date varchar2(10) := '2026-01-01';
  begin
    select date '2026-01-01' from dual§;
  end;
  ```
- **Expected**: `select date '2026-01-01' from dual` (`date '...'` is an ANSI date literal keyword).
- **Actual**: `select :date '2026-01-01' from dual` (invalid SQL syntax error).
- **Root Cause**: In `Substitute`, `src.IsName(k)` matches `DATE` in `DATE '2026-01-01'`. `Lookup` checks `InScope` for `DATE` and finds the declared local variable `date`. `Substitute` converts the keyword `DATE` into bind variable `:date`.
- **Concrete Fix**: In the skip condition of [ConstantResolver.cs:261](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L261), skip name tokens that serve as literal type prefixes:
  ```csharp
  || (k + 1 < to && src.Tokens[k + 1].Kind == TokenKind.StringLiteral && src.Keyword(k) is "DATE" or "TIMESTAMP" or "INTERVAL")
  ```

---

### Bug 8: `Exports.OnActivate` duplicates context menu items and ribbon buttons if re-activated
- **Severity**: **Minor**
- **File:Line**: [Exports.cs:73-87](file:///src/TextTools.Plugin/Exports.cs#L73-L87) (`OnActivate`)
- **Minimal Repro**: Disable and re-enable the plug-in in PL/SQL Developer (Configure → Plug-Ins), or trigger `OnActivate()` more than once.
- **Expected**: Exactly 9 items in editor context menus (`Commands.ContextMenu`) and 3 ribbon buttons.
- **Actual**: `IDE_CreatePopupItem` (callback 69) and `IDE_CreateToolButton` (callback 150) are called unconditionally on every activation. Delphi's `TPopupMenu.Items.Add` appends new items each time, resulting in duplicated context menu entries and duplicate ribbon icons.
- **Concrete Fix**: Add a static initialization flag in `Exports.cs`:
  ```csharp
  private static bool _popupItemsCreated;
  ...
  if (!_popupItemsCreated)
  {
      _popupItemsCreated = true;
      foreach (var name in Commands.ContextMenu)
      {
          if (Menu.IndexOf(name) is not { } index) continue;
          foreach (var window in (string[])["PROGRAMWINDOW", "SQLWINDOW", "TESTWINDOW"])
          {
              Ide.CreatePopupItem(_pluginId, index, name, window);
          }
      }
      if (Ide.SysVersion >= 1200)
      {
          foreach (var (index, name) in Menu.Buttons)
          {
              if (RibbonIcons.FileFor(name) is { } file) Ide.CreateToolButton(_pluginId, index, name, file);
          }
      }
  }
  ```

---

### Bug 9: `.github/workflows/real-ide.yml` version mismatch between upload and download artifact actions
- **Severity**: **Minor**
- **File:Line**: [.github/workflows/real-ide.yml:43, 69, 108](file:///.github/workflows/real-ide.yml#L43)
- **Minimal Repro**: Inspect workflow definition.
- **Expected**: Artifact upload and download actions should use compatible versions across workflow jobs.
- **Actual**: Job `build` uploads with `actions/upload-artifact@v7.0.2`, while job `ide` downloads with `actions/download-artifact@v4`.
- **Concrete Fix**: Standardize artifact actions to `@v4` (or the project's consistent release tag).

---

### Bug 10: `.github/workflows/real-ide.yml` `paths` filter ignores changes in `src/TextTools.Core/**`
- **Severity**: **Minor**
- **File:Line**: [.github/workflows/real-ide.yml:17-21](file:///.github/workflows/real-ide.yml#L17-L21)
- **Minimal Repro**: Create a PR modifying only `src/TextTools.Core/Sql/ConstantResolver.cs`.
- **Expected**: PR triggers the real IDE smoke test workflow to ensure core changes do not break within a real PL/SQL Developer process.
- **Actual**: Workflow triggers only on `src/TextTools.Plugin/**`, `scripts/real-ide-smoke.ps1`, and `.github/workflows/real-ide.yml`. Changes to Core bypass the real IDE gate.
- **Concrete Fix**: Add `- src/TextTools.Core/**` to the `pull_request.paths` filter.

---

## 2. Design Risks & Recommendations

### Database Lookups (`DatabaseSpecs.cs`, `SpecQuery.cs`, `Ide.cs`)
1. **Unbounded Static Cache**:
   - `DatabaseSpecs.Cache` is a static `Dictionary<string, (DateTime At, string? Spec)>`. While a 5-second expiration check is performed on lookup, expired entries are never pruned. Over prolonged IDE sessions where many packages are inspected, memory will grow monotonically.
   - *Recommendation*: Evict stale entries when `Cache.Count > 100`, or use a simple bounded LRU cache.
2. **Case Sensitivity in `DatabaseSpecs.Cache`**:
   - `DatabaseSpecs.Cache` uses `StringComparer.Ordinal` on key `"{owner}.{package}"`. `_window.Owner` from `Ide.GetWindowObject()` is typically uppercase (e.g. `HR`), but unquoted code references might be upper or lower depending on lexical extraction. Standardize cache keys to uppercase ordinal for unquoted identifiers.
3. **PAnsiChar Out-Parameters in Callback 110 (`IDE_GetWindowObject`)**:
   - `Ide.GetWindowObject()` in `Ide.cs:96` passes pointers to pointers (`&type, &owner, &name, &sub`) with signature `(byte**, byte**, byte**, byte**) -> int`. This matches Delphi's `var ObjectType, ObjectOwner, ObjectName, SubObject: PAnsiChar` cdecl calling convention where Delphi populates the caller-provided pointer variables with pointers to its internal string memory. The use of `FromNative` without freeing is correct because the IDE owns the underlying memory.
4. **Main Session Execution Safety**:
   - `DatabaseSpecs.IfConnected()` verifies `!Ide.Connected || Ide.WindowIsRunning`. This protects the IDE main session from executing dictionary queries while user statements are executing, upholding the hard rule "never crash the IDE / never freeze execution".

### GitHub Actions Real IDE Matrix (`real-ide.yml`)
1. **Upstream Installer URL Volatility**:
   - The matrix references exact MSI filenames (e.g. `plsqldev1703x64.msi`). Allround Automations frequently removes older minor point releases when new ones are published. While `actions/cache@v4` protects against repeated downloads, a cache eviction combined with a vendor patch release will cause `curl -fsSL` to fail with HTTP 404.
   - *Recommendation*: Document the upgrade procedure in `scripts/real-ide-smoke.ps1` or monitor installer link health.
2. **Oracle Instant Client Rolling Zip**:
   - `https://download.oracle.com/otn_software/nt/instantclient/instantclient-basiclite-windows.zip` is a floating URL. It is frozen via cache key `oracle-instantclient-basiclite-windows-x64-v1`. If the key suffix is bumped, verify that folder naming inside the archive still matches `instantclient_*`.

---

## 3. What Was Checked and Found Correct

1. **Indexed Dictionary Queries in `SpecQuery`**:
   - `SpecQuery.Source` uses indexed lookups on `USER_SOURCE` (for unqualified packages) and `ALL_SOURCE` (for qualified packages). No slow joins against `ALL_OBJECTS` are performed.
   - `SpecQuery.Synonym` queries `ALL_SYNONYMS` filtering by `owner in (sys_context('USERENV', 'CURRENT_SCHEMA'), 'PUBLIC')` and sorting private synonyms before public synonyms (`order by case owner when 'PUBLIC' then 2 else 1 end`), accurately mirroring Oracle synonym precedence.
2. **Function Call Bypass**:
   - `pkg.f(x)` and `rec.tab(i)` are detected via `called && parts.Count > 1` in `Substitute` ([ConstantResolver.cs:275-276](file:///src/TextTools.Core/Sql/ConstantResolver.cs#L275-L276)) and bypassed before calling `Lookup` or querying `ISpecSource`.
3. **Lookup Rate Limiting**:
   - `MaxSpecLookups` (50) correctly stops dictionary queries on statements referencing numerous unknown qualifiers, preventing IDE freezes on large scripts.
4. **Package Variable Trust Model**:
   - Package-level variables with initial values (`g_x number := 10;`) are only substituted if the value expression is a literal or single name (`!d.IsConstant && !single && !literal` returns `null`). Non-literal expressions (`10 + 20`) and constructor calls (`t_tab()`) safely become bind variables.
5. **Local vs Package-Level Variables**:
   - Local variables inside procedure bodies (`l_x number := 10;`) are never assigned `HasValue`, always becoming bind variables (`:l_x`).
   - Package body globals (`g_val number := 10;`) resolve accurately within procedures in the same package body.
6. **Subprogram Declarations & Forward Declarations**:
   - Forward declarations (`procedure helper;` and `procedure helper(p in number);`) do not push frames to the stack, allowing subsequent package variables and constants to be parsed cleanly.
   - Nested procedure declarations create isolated scopes; inner constants shadow outer variables and do not leak beyond `END <subprogram>;`.
7. **Boolean Safety**:
   - `Declaration.IsBoolean` and literal `TRUE`/`FALSE` checks prevent boolean constants from being substituted into SQL expressions, emitting bind variables instead (`:c_flag`).
8. **Crash Safety & Interop**:
   - Every plugin export maintains `try/catch` with static fallback strings. Core throws only `FormatException`.
