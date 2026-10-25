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

### 1. The Beautifier does not follow your code standard

`END name;`, `:=` in columns, `INSERT` / `SELECT` / `JOIN` layout, long lines, blank lines. **Format Tools** adds these rules after the Beautifier. One checkbox per rule, a live preview, and the rules can be exported to a file and shared with the team.

<p align="center"><img src="docs/assets/format.png" alt="Before and after Format Tools" width="880"></p>
<p align="center"><img src="docs/assets/settings.png" alt="Format Tools settings window" width="640"></p>

### 2. A query with package constants does not run

**Copy SQL with constants resolved**: `c_new` becomes `'NEW'`, variables become binds, `INTO` is removed. Paste it into a SQL Window or Explain Plan.

<p align="center"><img src="docs/assets/copy-sql.png" alt="Copy SQL with constants resolved" width="880"></p>

### 3. ORA-01795: more than 1000 values in `IN`

Paste a list (lines or `1, 2, 3`), get `IN (...)` split into groups that Oracle accepts.

<p align="center"><img src="docs/assets/in-list.png" alt="List to IN (...)" width="420"></p>

### 4. Small chores, one click

camelCase / snake_case, quotes and `q'[...]'`, Base64 / HEX / URL, sorting lines, the current date as `TO_DATE`, epoch and Java/.NET date masks. All commands: [commands.md](docs/commands.md).

> The code itself never changes: tokens are compared before and after, and a rule that would touch more than layout is skipped.

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
