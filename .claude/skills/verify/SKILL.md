---
name: verify
description: Runs and interprets every quality gate of Text Tools — dotnet format, build with .NET/Meziantou/Sonar analyzers as errors, unit tests with the coverage gate, vulnerable packages, Native AOT publish, exported functions, and the host emulator (smoke, nocallbacks, noutf8, unload, stress). Use before every commit/PR, after any code change, and when CI fails.
---

# Verification

Run from the repo root (same script as the `verify` CI job):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
```

It stops at the first failing step. Prerequisites: .NET 10 SDK; VS 2022 Build Tools with the C++ workload (Native AOT
linker). The script puts `...\Microsoft Visual Studio\Installer` on PATH because ILCompiler looks for `vswhere.exe`.

| Step | Fails when | What to do |
|---|---|---|
| format | whitespace/style differs, LF expected | `dotnet format TextTools.slnx`, review the diff. Python/PowerShell writes on Windows produce CRLF: write files with LF |
| build | any warning (TreatWarningsAsErrors) | Fix the code. Suppress only in `.editorconfig` for a path, with a reason comment (see the existing `tools/**` block). Never `#pragma` in product code |
| tests + coverage | a test fails or Core line coverage < 95% (`*.g.cs` excluded) | add tests; never lower the threshold to pass |
| vulnerable packages | a NuGet advisory | bump the version in `Directory.Packages.props` |
| publish | AOT/trim warning, linker missing | AOT-incompatible API (reflection, dynamic code): replace it |
| exports | a C export missing | check `[UnmanagedCallersOnly(EntryPoint = ...)]` in `Exports.cs` |
| host emulator | a `FAIL` line | run the single mode: `dotnet run --project tools/HostEmulator -c Release -- <dll> smoke` |

Emulator modes: `smoke` (end-to-end commands, status texts), `nocallbacks` (IDE registered nothing),
`noutf8` (IDE refused UTF-8), `unload` (FreeLibrary ×3 + GC pressure: module must stay pinned),
`stress` (~150k random clicks, memory must not grow).

Useful one-offs:
- Only analyzers on one project: `dotnet build src/TextTools.Core`.
- Unique diagnostics from a log: `dotnet build TextTools.slnx > build.log` then group by rule id.
- Coverage details: `tests/TextTools.Core.Tests/coverage.cobertura.xml` (uncovered lines have `hits="0"`).

Green locally does not prove the IDE behavior: interop changes also need skill `install-to-ide`.
