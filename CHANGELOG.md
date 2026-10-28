# Changelog

All notable changes to Text Tools for PL/SQL Developer. Versions follow [Semantic Versioning](https://semver.org/):
PATCH = fixes, MINOR = new commands, MAJOR = removed or renamed commands (menu captions are public API: users bind
hotkeys to them) or a dropped IDE version. Breaking changes are listed under **Breaking**.

A section is drafted with `sh scripts/release-notes.sh <previous tag> <version>` and edited in the release PR.

## [Unreleased]

## [v0.4.0] - 2026-10-28

### Features

- **Format Tools** follows the bank's code standard out of the box (right margin 76, lines up to 120): the defaults of the
  settings are the standard's, and 17 more layout rules join the post-pass after the Beautifier, each one a switch in
  the settings window (new tabs "SQL" and "Wrapping"): INSERT / SELECT / SELECT ... INTO / UPDATE SET / JOIN ... ON /
  subquery layouts, IF conditions on one logical line, wrapping of lines past the margin (DECODE in pairs, BETWEEN,
  `||` chains, named arguments with `=>` aligned), aligned declarations, comment indent, subprogram header tails on one
  line, a blank after each comma (`SUBSTR(x, 1, 3)`), and an optional blank line after the package header.
- **Format Tools** joins a call that the Beautifier broke over several lines when it fits the margin, lays out
  `JSON_OBJECT(... VALUE ...)` with the pairs one per line (nested calls too), breaks a long `||` chain at the operator
  and never inside a call, keeps a line that only its closing semicolon pushes past the margin on one line, and cuts the
  arguments of a multi-line call only past 120 characters. Aligned aliases that would push a line past 120 characters
  get one blank before them.
- **Format Tools -> Settings**: the settings are laid out in columns, the preview shows the effect of every option, the
  wrapping options are greyed out while wrapping is off. New buttons **Export...** and **Import...** save the rules to a
  `.ttr` file (`Key=Value` like the Beautifier's `.br`) and load them; importing a Beautifier `.br` takes the keys both
  have (right margin, aligned declarations, aligned assignments). A new ribbon button icon for Format Tools.
- **Format Tools** (developer command): in a Command Window `plugin PlsqlDevTextTools format <input folder> <Beautifier
  output folder> <result folder>` runs every package, type and body file of a folder through the IDE's Beautifier and
  then through the Text Tools rules and writes both results; it is how the whole formatting is compared with another
  formatter on the same files.
- The README is rewritten (what it solves, four feature sections with screenshots, a new demo) and translated into
  Russian, Uzbek, Spanish, Portuguese, Chinese, Japanese and Hindi.

### Fixes

- A select list with a column alignment that reached past 120 characters, a `SUBSTR(SQLERRM, 1, 150)` split argument by
  argument inside a long `||` chain, and `IN  CLOB` parameter lists squeezed to one blank are laid out as the standard
  says. The tokens, string literals and comments are still verified before and after; a rule whose result would change
  any of them is skipped on its own.

## [v0.3.0] - 2026-10-23

### Features

- **Format Tools → Apply formatting rules**: formatting rules that the IDE's Beautifier cannot express, applied to the
  selection or to the whole text after the Beautifier has run. Everything happens inside the DLL: no external tools, no
  database. The rules: `END name;` for named subprograms and packages; `:=` aligned in groups; Oracle built-in
  functions and `%TYPE` / `%ROWTYPE` in upper case; a blank line before each PROCEDURE and FUNCTION (and its comment)
  but not before the closing `END` of a package; a long or multi-line `EXECUTE IMMEDIATE` literal on its own line; no
  trailing blanks. Only layout and case change: the tokens are compared before and after, and if anything else would
  change, the text is left as it was.
- **Format Tools → Settings**: a window like the IDE's Beautifier Rules. A tab per group (General, Case, Code, Blank
  lines), a checkbox or a number per rule, and a large preview of a sample with line numbers and colours that follows
  every click. The window can be resized. The settings are saved in `%APPDATA%\TextTools\format-rules.json`: put the
  file into a repository and copy it to each machine, and the menu command uses it. The window follows the language of
  the IDE.
- **Menu in the language of the IDE**: submenu and item captions (Plug-Ins menu, ribbon, editor context menu) follow
  the IDE's translation (Preferences → Language): Brazilian, Chinese, French, German, Hungarian, Japanese, Russian,
  Spanish; English when the IDE has no translation. If a Key Configuration hotkey of a Text Tools item stops working
  after switching the IDE language, bind it again.
- **SQL Tools → Explain Plan with constants**: the IDE's own Explain Plan of the statement under the cursor, with
  constants resolved like Copy SQL (bind F5 to it in Key Configuration to replace the built-in one).
- **SQL Tools**: no separator line between Copy SQL with constants resolved and Explain Plan with constants.

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
