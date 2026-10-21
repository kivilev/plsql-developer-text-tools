# Changelog

All notable changes to Text Tools for PL/SQL Developer. Versions follow [Semantic Versioning](https://semver.org/):
PATCH = fixes, MINOR = new commands, MAJOR = removed or renamed commands (menu captions are public API: users bind
hotkeys to them) or a dropped IDE version. Breaking changes are listed under **Breaking**.

A section is drafted with `sh scripts/release-notes.sh <previous tag> <version>` and edited in the release PR.

## [Unreleased]

### Features

- **SQL Tools → Explain Plan with constants**: the IDE's own Explain Plan of the statement under the cursor, with
  constants resolved like Copy SQL (bind F5 to it in Key Configuration to replace the built-in one).
- **SQL Tools → Extended Explain Plan**: an HTML window that explains the plan of the statement under the cursor
  without AI, from the plan itself and the data dictionary. Running it again refreshes the same window.
  - **Plan** tab: steps in reading order (children first) with "runs for each row of step N" on the inner side of
    nested loops and FILTER subqueries, scalar subqueries and hash join build/probe sides; Rows and Cost as log bars;
    a panel per step: the step in plain words, access/filter predicates, table size and statistics, indexes with the
    predicate columns underlined. Inactive rows of an adaptive plan are left out (OTHER_XML display map).
  - **Findings** tab: 19 static rules - conversions and functions on indexed columns, DATE vs TIMESTAMP, joins of
    different types, linguistic comparison, full scan of a big table for a few rows, index read without an access
    condition, index rows discarded by the table filter, unusable/invisible index, Cartesian join, full scan or a
    database link inside nested loops, no partition pruning, FILTER subquery per row, hint errors and unused hints
    (Hint Report), dynamic sampling (with the statistics from the dictionary), fixed plans, adaptive plans, collections
    with the default estimate. Each finding says when it may be fine; small tables never raise full scan alarms.
  - **Text** tab: the raw `DBMS_XPLAN` output (`ADVANCED +ADAPTIVE +HINT_REPORT` by version) with a Copy button.
  - **Reference** tab and tooltips: what each operation and column means; banners for bind variables, adaptive and
    fixed plans. Rules and texts are data (`Plan/reference.json` in the DLL). Read-only: EXPLAIN PLAN, DBMS_XPLAN and
    `ALL_*` views through the IDE's own connection.
  - **Language**: the report speaks the language of the IDE's translation (Preferences → User Interface → Language):
    Brazilian, Chinese, French, German, Hungarian, Japanese, Russian, Spanish; English otherwise. The texts are
    `Plan/reference.<code>.json` laid over `reference.json`; a missing key shows the English text.
  - The statement is shown above the plan table, not below it.
- **Menu in the language of the IDE**: submenu and item captions (Plug-Ins menu, ribbon, editor context menu) follow
  the IDE's translation, like the plan report; English when the IDE has no translation. If a Key Configuration hotkey
  of a Text Tools item stops working after switching the IDE language, bind it again.
- **SQL Tools**: no separator between Copy SQL with constants resolved and the two Explain Plan commands.

## [v0.2.0] - 2026-10-17

### Features

- **SQL Tools → Copy SQL with constants resolved**: put the cursor in a query inside a package, procedure or block (or
  select it) and get a statement that runs on its own in a SQL Window or Explain Plan. PL/SQL constants become their
  values (`status = c_new` → `status = 'NEW' /*c_new*/`), variables and parameters become bind variables (`:p_id`),
  `INTO` / `BULK COLLECT INTO` and `RETURNING ... INTO` are removed. The editor is not changed; the result goes to the
  clipboard.
  - Constants are found like PL/SQL does: enclosing blocks, the package body, its specification, `pkg.c_x` and
    `owner.pkg.c_x`; package variables with a plain initial value count too. When the IDE is connected, other packages'
    specifications are read from the database through the IDE's own connection (no other tools needed).
  - A constant whose value is still unknown becomes a bind variable named like it (`pkg.c_x` → `:c_x`).
  - Columns, tables, aliases, sequences and function calls are left as they are.
- **Context menu**: Copy SQL and the everyday commands (IN list, quoting, case, dates) are also in the editor's
  right-click menu.
- Icons on the ribbon buttons.

### Internal

- Tested inside real PL/SQL Developer 12, 13, 14, 15, 16 and 17 on every interop change: CI installs each version and
  runs the plug-in self test in it (also available by hand: `TextTools selftest` in a Command Window).

## [v0.1.0] - 2026-10-14

First release.

### Features

- **String Tools**: camelCase, PascalCase, snake_case, UPPER_SNAKE_CASE, kebab-case for identifiers (with `$`, `#`, Cyrillic);
  escape / unescape quotes, `q'[...]'`, a list (lines, or one line split by spaces, commas, tabs) to `IN (...)` beyond 1000 values (no ORA-01795), lines to `'a' || chr(10) || 'b'`;
  Base64, URL, HEX (UTF-8) encode / decode; sort, reverse, remove duplicates / empty lines / trailing spaces, align by `:=` and `=>`.
- **Date Tools**: now as `DATE`, `TIMESTAMP`, `TIMESTAMP WITH TIME ZONE`, `TO_DATE(...)`, ISO 8601, Unix epoch
  (from `SYSTIMESTAMP` when connected); epoch to `TIMESTAMP`, date to epoch / `TO_DATE` / literal;
  Java and .NET date formats to Oracle masks and back; ISO 8601 duration to `INTERVAL`.
- The status bar shows an example for each command; invalid input leaves the text unchanged and explains why.
- Native x64 DLL built with .NET Native AOT: no runtime to install.
