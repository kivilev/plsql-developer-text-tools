# Text Tools for PL/SQL Developer

A native 64-bit plug-in for [Allround Automations PL/SQL Developer](https://www.allroundautomations.com/products/pl-sql-developer/)
that transforms the selected editor text — in the spirit of StringManipulation for IntelliJ, tuned for Oracle SQL and PL/SQL.

> Status: pre-release, private. Tested on PL/SQL Developer 14 (x64).

## Features

**Plug-Ins → String Tools**

- Case: camelCase, PascalCase, snake_case, UPPER_SNAKE_CASE, kebab-case (identifiers only; `$`, `#`, Cyrillic supported)
- SQL: escape / unescape quotes, wrap in `q'[...]'`, lines → `IN (...)` (over 1000 values via
  `sys.odcivarchar2list` / `sys.odcinumberlist`, no ORA-01795), lines → `'a' || chr(10) || 'b'`
- Encode: Base64, URL, HEX (UTF-8, same as `UTL_RAW.CAST_TO_RAW` on AL32UTF8)
- Lines: sort, reverse, remove duplicates / empty lines / trailing spaces, align by `:=` and `=>`

**Plug-Ins → Date Tools**

- Now: `DATE`, `TIMESTAMP`, `TIMESTAMP WITH TIME ZONE`, `TO_DATE(...)`, ISO 8601, Unix epoch — from `SYSTIMESTAMP`
  when connected, otherwise the PC clock
- Convert (line by line): Unix epoch (s/ms/µs/ns) → `TIMESTAMP`, date → epoch, date → `TO_DATE` / `TO_TIMESTAMP` /
  `TO_TIMESTAMP_TZ`, date → `DATE` / `TIMESTAMP` literal
- Masks: Java / .NET date format ↔ Oracle format mask (`yyyy-MM-dd'T'HH:mm:ss.SSSXXX` → `YYYY-MM-DD"T"HH24:MI:SS.FF3TZH:TZM`),
  ISO 8601 duration → `INTERVAL` (`PT1H30M` → `INTERVAL '1:30' HOUR TO MINUTE`)

The status bar shows an example of each command. Invalid input never changes the text: the status bar explains why.

## Install

1. Close PL/SQL Developer.
2. Copy `TextTools.dll` into `<PL/SQL Developer>\PlugIns` (administrator rights for Program Files).
3. Start the IDE; if needed enable it in Configure → Plug-Ins.

No .NET runtime is required: the DLL is compiled with .NET Native AOT.

## Build

Requirements: Windows x64, .NET 10 SDK, Visual Studio 2022 Build Tools with the C++ workload.

```powershell
powershell -File scripts/verify.ps1      # format, analyzers, tests, coverage, AOT publish, host emulator
```

The DLL: `src/TextTools.Plugin/bin/Release/net10.0/win-x64/publish/TextTools.dll`.

## Contributing

Bugs and ideas: GitHub issues (templates provided). Commit and PR rules: [CLAUDE.md](CLAUDE.md#git-workflow).
