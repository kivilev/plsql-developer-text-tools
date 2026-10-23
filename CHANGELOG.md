# Changelog

All notable changes to Text Tools for PL/SQL Developer. Versions follow [Semantic Versioning](https://semver.org/):
PATCH = fixes, MINOR = new commands, MAJOR = removed or renamed commands (menu captions are public API: users bind
hotkeys to them) or a dropped IDE version. Breaking changes are listed under **Breaking**.

A section is drafted with `sh scripts/release-notes.sh <previous tag> <version>` and edited in the release PR.

## [Unreleased]

### Features

- **SQL Tools → Explain Plan with constants**: the IDE's own Explain Plan of the statement under the cursor, with
  constants resolved like Copy SQL (bind F5 to it in Key Configuration to replace the built-in one).
- **Menu in the language of the IDE**: submenu and item captions (Plug-Ins menu, ribbon, editor context menu) follow
  the IDE's translation (Preferences → Language): Brazilian, Chinese, French, German, Hungarian, Japanese, Russian,
  Spanish; English when the IDE has no translation. If a Key Configuration hotkey of a Text Tools item stops working
  after switching the IDE language, bind it again.
- **SQL Tools**: no separator line between Copy SQL with constants resolved and Explain Plan with constants.
- **Extended Explain Plan** is taken out of the menu until it is stable (work continues in a draft pull request).
- **Format Tools → Apply formatting rules**: formatting rules that the IDE's Beautifier cannot express, applied to the
  selection or the whole text after the Beautifier ran. Everything is inside the DLL (no external tools, no database).
  Rules: `END name;` for named subprograms and packages, `:=` aligned in groups, Oracle built-in functions and `%TYPE`
  / `%ROWTYPE` in upper case, a blank line before each PROCEDURE/FUNCTION (and its comment) but not before the closing
  `END` of a package, a long or multi-line `EXECUTE IMMEDIATE` literal on its own line, no trailing blanks. Only layout
  and case change: the tokens are compared before and after, and on any difference the text is left as it was.
- **Format Tools → Settings**: a window like the IDE's Beautifier Rules: a tab per group (General, Case, Code, Blank
  lines), a checkbox or number per rule, the text of a sample **Before** and **After** that follows every click, and
  Save / Default / Close. The profile is a JSON file (`%APPDATA%\TextTools\format-rules.json`) that can be put into a
  repository and copied to each machine; the menu command uses the saved profile. The window and its texts follow the
  language of the IDE. Settings that several IDEs share (right margin, align assignments, ...) are one setting that
  remembers its names in PL/SQL Developer, SQL Developer and DataGrip, so that their profiles can be imported later.

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
