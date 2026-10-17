<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/assets/logo-dark.svg">
    <img src="docs/assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">
  Convert the selected text in <a href="https://www.allroundautomations.com/products/pl-sql-developer/">PL/SQL Developer</a>:
  identifier case, SQL quoting, <code>IN</code> lists over 1000 values, Base64/HEX,
  Oracle date literals and Java/.NET date masks. A native 64-bit plug-in, no runtime to install.
</p>

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

<p align="center"><img src="docs/assets/demo.gif" alt="Demo: values to IN (...), identifier case, current time from the database, date to TO_DATE" width="880"></p>

## Features

Three submenus under **Plug-Ins**. Every command works on the selection (or the whole text when nothing is selected),
shows an example in the status bar and never changes the text when the input is invalid.
The full list with examples: [docs/commands.md](docs/commands.md).

**SQL Tools → Copy SQL with constants resolved.** Put the cursor in a query inside a package, procedure or block
(or select it) and get a statement that runs on its own in a SQL Window or Explain Plan: PL/SQL constants become their
values (`status = c_new` → `status = 'NEW' /*c_new*/`), variables and parameters become bind variables (`:p_id`),
`INTO` / `BULK COLLECT INTO` and `RETURNING ... INTO` are removed. Constants are found like PL/SQL does: enclosing
blocks, the package body, its specification, `pkg.c_x` from package specifications in the same editor; when the IDE is
connected, other packages' specifications are read from the database (`ALL_SOURCE`, synonyms followed). A constant whose
value is still unknown becomes a bind variable named like it (`pkg.c_x` → `:c_x`). The editor is not changed; the
result goes to the clipboard.

| String Tools | Date Tools |
|---|---|
| **Case**: `first_name` → camelCase, PascalCase, snake_case, UPPER_SNAKE_CASE, kebab-case (`$`, `#`, Cyrillic supported) | **Now**: insert the current `DATE`, `TIMESTAMP`, `TIMESTAMP WITH TIME ZONE`, `TO_DATE(...)`, ISO 8601 or Unix epoch — from `SYSTIMESTAMP` when connected |
| **SQL**: escape / unescape quotes, `q'[...]'`, a list (lines or `1 2 3`, `a, b`) → `IN (...)` beyond 1000 values (no ORA-01795), lines → `'a' \|\| chr(10) \|\| 'b'` | **Convert**: epoch (s/ms/µs/ns) → `TIMESTAMP`, date → epoch, `13.10.2026 14:30` → `TO_DATE('2026-10-13 14:30:00', 'YYYY-MM-DD HH24:MI:SS')` or a `DATE`/`TIMESTAMP` literal |
| **Encode**: Base64, URL, HEX (UTF-8, as `UTL_RAW.CAST_TO_RAW`) | **Masks**: `yyyy-MM-dd'T'HH:mm:ss.SSSXXX` ↔ `YYYY-MM-DD"T"HH24:MI:SS.FF3TZH:TZM` (Java and .NET), `PT1H30M` → `INTERVAL '1:30' HOUR TO MINUTE` |
| **Lines**: sort, reverse, remove duplicates / empty lines / trailing spaces, align by `:=` and `=>` | |

## Install

1. Download `PlsqlDevTextTools.dll` from the [latest release](https://github.com/kivilev/plsql-developer-text-tools/releases/latest).
2. Close PL/SQL Developer.
3. Copy `PlsqlDevTextTools.dll` into `<PL/SQL Developer>\PlugIns` (for example `C:\Program Files\PLSQL Developer 16\PlugIns`;
   Windows asks for administrator rights there).
4. Start PL/SQL Developer. If **Plug-Ins → String Tools / Date Tools** is missing, enable *Text Tools for PL/SQL Developer*
   in **Configure → Plug-Ins** and restart.

Downloaded the zip instead? Unpack it and run `Unblock-File PlsqlDevTextTools.dll` in PowerShell before copying.
Checksums are in `SHA256SUMS.txt` next to every release.

**Upgrade**: close the IDE, replace `PlsqlDevTextTools.dll`, start the IDE. **Uninstall**: close the IDE, delete `PlsqlDevTextTools.dll`.

## Keyboard shortcuts

Any command can get a hotkey: **Configure → Preferences → User Interface → Key Configuration**, type `Text Tools` in the
filter, select a command (`Plug-Ins / Text Tools / String Tools / snake_case`), press the keys (e.g. `Ctrl+Shift+U`),
**OK**. Command names do not change between versions (a rename would be listed under **Breaking** in the changelog),
so your shortcuts survive upgrades.


| PL/SQL Developer | Status |
|---|---|
| 14 x64 | tested |
| 12, 13, 15, 16 x64 | expected to work (same plug-in interface), not tested yet |
| 32-bit editions | not supported |

A report that it works (or not) on your version is welcome in [Discussions](https://github.com/kivilev/plsql-developer-text-tools/discussions).

## FAQ

**Do I need .NET?** No. The DLL is compiled with .NET Native AOT and has no runtime dependency.

**Why only 64-bit?** PL/SQL Developer 16 is 64-bit only, and 32-bit plug-ins need a separate build. Ask in Discussions if you need it.

**What happens without a selection?** Text commands apply to the whole editor text; "Now" commands insert at the cursor.

**Can it break my script?** Invalid input (an unrecognized date, bad Base64, an ambiguous `01/02/2026`) never changes the text:
the status bar explains why. The plug-in is fuzz-tested and checked in a host emulator for crashes and memory leaks.

**My antivirus complains.** The DLL is not code-signed yet. Compare its SHA-256 with `SHA256SUMS.txt`, check the build
provenance (`gh attestation verify PlsqlDevTextTools.dll -R kivilev/plsql-developer-text-tools`) and report a false positive to the vendor.

**Does it send anything anywhere?** No network access. When connected it runs one query, `SYSTIMESTAMP` from `dual`, for the "Now" commands.

## Build from source

Requirements: Windows x64, .NET 10 SDK, Visual Studio 2022 Build Tools with the C++ workload.

```powershell
powershell -File scripts/verify.ps1      # format, analyzers, tests, coverage, Native AOT publish, host emulator
```

The DLL: `src/TextTools.Plugin/bin/Release/net10.0/win-x64/publish/PlsqlDevTextTools.dll`.

## Contributing

Questions and ideas: [Discussions](https://github.com/kivilev/plsql-developer-text-tools/discussions).
Bugs and feature requests: [issues](https://github.com/kivilev/plsql-developer-text-tools/issues/new/choose).
Code: see [CONTRIBUTING.md](CONTRIBUTING.md). Changes: [CHANGELOG.md](CHANGELOG.md).

## Author

**Denis Kivilev**: backend developer since 2005, Oracle Certified Professional (Advanced PL/SQL Developer, SQL Expert),
founder of the [Backend PRO](https://backend-pro.ru) online school.
Courses for Oracle developers (in Russian): [Oracle PL/SQL Master](https://backend-pro.ru/master-plsql),
Oracle SQL performance tuning and more in the [catalog](https://backend-pro.ru/courses).

## License

[MIT](LICENSE) © 2026 Denis Kivilev

PL/SQL Developer is a trademark of Allround Automations. Oracle and PL/SQL are trademarks of Oracle Corporation.
This project is not affiliated with or endorsed by either company.
