<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/assets/logo-dark.svg">
    <img src="docs/assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">Fixes what PL/SQL Developer's Beautifier leaves undone, makes a query from a package run on its own, and removes small text chores. A native 64-bit plug-in, nothing to install but one DLL.</p>

<p align="center"><b>English</b> &middot; <a href="docs/readme/README.ru.md">Русский</a> &middot; <a href="docs/readme/README.uz.md">Oʻzbekcha</a> &middot; <a href="docs/readme/README.es.md">Español</a> &middot; <a href="docs/readme/README.pt.md">Português</a> &middot; <a href="docs/readme/README.zh.md">中文</a> &middot; <a href="docs/readme/README.ja.md">日本語</a> &middot; <a href="docs/readme/README.hi.md">हिन्दी</a></p>

<p align="center">
  <a href="https://github.com/kivilev/plsql-developer-text-tools/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/kivilev/plsql-developer-text-tools/actions/workflows/ci.yml/badge.svg?branch=master"></a>
  <a href="https://github.com/kivilev/plsql-developer-text-tools/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/kivilev/plsql-developer-text-tools?sort=semver"></a>
  <a href="https://github.com/kivilev/plsql-developer-text-tools/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/kivilev/plsql-developer-text-tools/total"></a>
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/github/license/kivilev/plsql-developer-text-tools"></a>
  <br>
  <img alt="Windows x64" src="https://img.shields.io/badge/platform-Windows%20x64-0078D6?logo=windows">
  <img alt="PL/SQL Developer 12+ x64" src="https://img.shields.io/badge/PL%2FSQL%20Developer-12%2B%20x64-6E7781">
  <img alt=".NET 10 Native AOT" src="https://img.shields.io/badge/.NET%2010-Native%20AOT-512BD4?logo=dotnet">
</p>

<p align="center"><img src="docs/assets/demo.gif" alt="Demo" width="880"></p>

## What it solves

### 1. Extended Beautifier

*Format Tools*

Runs after the IDE's Beautifier and adds what its rules file cannot express: 25+ options, each one a checkbox, with a live preview. Rules can be exported to a file for the team (matching settings of the Beautifier's `.br` file can be imported).

- `END name;`, `:=` in columns, aligned declarations, comments at the indent of their code
- layout of `INSERT`, `SELECT`, `SELECT ... INTO`, `UPDATE ... SET`, `JOIN ... ON`, subqueries
- long lines wrapped at the margin: `DECODE` in pairs, `BETWEEN`, `||`, `=>` aligned
- `IF` conditions on one logical line, short groups in parentheses on one line
- built-in functions and `%TYPE` in upper case, a blank after each comma (`SUBSTR(x, 1, 3)`)
- blank lines between subprograms, none before the closing `END`, `EXECUTE IMMEDIATE` layout, no trailing blanks
- the code never changes: tokens are compared before and after, a rule that touches more than layout is skipped

<p align="center"><img src="docs/assets/format.png" alt="" width="880"></p>
<p align="center"><img src="docs/assets/settings-code.png" alt="" width="700"></p>
<p align="center"><img src="docs/assets/settings-sql.png" alt="" width="700"></p>
<p align="center"><img src="docs/assets/settings-wrapping.png" alt="" width="700"></p>

### 2. Copy a query with constants resolved

*SQL Tools*

Put the cursor in a query inside a package, procedure or block and get a statement that runs on its own in a SQL Window or Explain Plan.

- PL/SQL constants become their values: `status = c_new` → `status = 'NEW' /*c_new*/`
- variables and parameters become bind variables (`:i_customer`), `INTO`, `BULK COLLECT INTO` and `RETURNING ... INTO` are removed
- constants are found like PL/SQL does: block, package body, specification; other packages are read from the database when connected
- **Explain Plan with constants**: the same statement in the IDE's Plan Window

<p align="center"><img src="docs/assets/copy-sql.png" alt="" width="880"></p>
<p align="center"><img src="docs/assets/copy-sql-binds.png" alt="" width="880"></p>

### 3. String utilities

*String Tools*

Everything you retype by hand, in one click, on the selection or the whole text.

- **List → `IN (...)`** from lines, `1 2 3`, `a, b` or Excel; more than 1000 values do not hit ORA-01795
- **Case**: camelCase, PascalCase, snake_case, UPPER_SNAKE_CASE, kebab-case (`$`, `#`, Cyrillic)
- **Quotes**: escape / unescape, `q'[...]'`, lines → `'a' || chr(10) || 'b'`
- **Encode**: Base64, URL, HEX
- **Lines**: sort, reverse, remove duplicates and empty lines, trim, align by `:=` and `=>`

<p align="center"><img src="docs/assets/in-list-big.png" alt="" width="880"></p>
<p align="center"><img src="docs/assets/string-case.png" alt="" width="480"></p>
<p align="center"><img src="docs/assets/string-quotes-encode.png" alt="" width="480"></p>

### 4. Date utilities

*Date Tools*

The current time and every date format you keep looking up.

- **Now**: `DATE`, `TIMESTAMP`, `TIMESTAMP WITH TIME ZONE`, `TO_DATE(...)`, ISO 8601, Unix epoch (from `SYSTIMESTAMP` when connected)
- **Convert**: epoch ↔ `TIMESTAMP`, `13.10.2026 14:30` → `TO_DATE(...)` or a `DATE` / `TIMESTAMP` literal
- **Masks**: Java and .NET `yyyy-MM-dd HH:mm` ↔ Oracle `YYYY-MM-DD HH24:MI`, `PT1H30M` → `INTERVAL`

<p align="center"><img src="docs/assets/date-now.png" alt="" width="880"></p>
<p align="center"><img src="docs/assets/date-convert.png" alt="" width="880"></p>

All commands with examples: [commands.md](docs/commands.md)

## Install

1. Download `PlsqlDevTextTools.dll` from the [latest release](https://github.com/kivilev/plsql-developer-text-tools/releases/latest).
2. Close PL/SQL Developer, copy the DLL into `<PL/SQL Developer>\PlugIns` (administrator rights), start the IDE.
3. If the menu is missing: **Configure → Plug-Ins**, enable *Text Tools*, restart.

PL/SQL Developer 12–17 x64, tested in CI on every version. No .NET needed, no network access.
Hotkeys: **Configure → Preferences → User Interface → Key Configuration**, filter `Text Tools`.

---

Questions: [Discussions](https://github.com/kivilev/plsql-developer-text-tools/discussions) · Bugs: [issues](https://github.com/kivilev/plsql-developer-text-tools/issues/new/choose) · Code: [CONTRIBUTING](CONTRIBUTING.md) · Changes: [CHANGELOG](CHANGELOG.md)

**Denis Kivilev**: backend developer since 2005, Oracle Certified Professional, founder of the [Backend PRO](https://backend-pro.ru) school.

**License:** [MIT](LICENSE) © 2026 Denis Kivilev

<sub>PL/SQL Developer is a trademark of Allround Automations; Oracle and PL/SQL are trademarks of Oracle Corporation. Not affiliated with either.</sub>
