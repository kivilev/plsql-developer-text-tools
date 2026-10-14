# Text Tools for PL/SQL Developer

Native x64 plugin for Allround Automations PL/SQL Developer in the spirit of StringManipulation (IntelliJ):
transformations of the selected editor text. Menu: Plug-Ins → **String Tools** (case, SQL quoting, IN lists
over 1000, Base64/URL/HEX, line tools) and **Date Tools** (now values from the DB, epoch/TO_DATE/literal
conversion, Java/.NET ↔ Oracle date masks, ISO duration → INTERVAL).

C# / .NET 10 **Native AOT** (`PublishAot`, `NativeLib=Shared`): one `PlsqlDevTextTools.dll`, no CLR in the IDE process,
exports via `[UnmanagedCallersOnly(EntryPoint = ...)]`. x64 only (PL/SQL Developer 16 is x64 only).
Development journal (in Russian): [docs/dev-journal.ru.md](docs/dev-journal.ru.md).

## Layout

```
src/TextTools.Core/        pure string -> string transformations; all logic and all tests target this
src/TextTools.Plugin/      interop: Exports (C exports), Ide (callbacks), Menu, Editor (apply to the editor), NativeStrings
tests/TextTools.Core.Tests/    xUnit: transforms, dates, robustness fuzzing of every command (coverage gate 95% lines)
tests/TextTools.Plugin.Tests/  xUnit: menu layout
tools/HostEmulator/        loads the native DLL like the IDE, fake callbacks; modes smoke|nocallbacks|noutf8|unload|stress|all
scripts/verify.ps1         every quality gate, same as CI
scripts/commit-rules.sh    commit / PR title / branch rules (hook + CI)
.claude/skills/            project skills (below)
```

## Rules

- **The plugin must never crash the IDE or corrupt the user's text.** Every export is wrapped in try/catch with
  static fallback strings; the module pins itself (`GetModuleHandleEx PIN`) because a Native AOT runtime cannot be
  unloaded; Core throws only `FormatException` (status bar), overflow is converted; every regex has
  `Limits.RegexTimeoutMs`; text with U+FFFD or non-ASCII without confirmed `CHARMODE=UTF8` is never rewritten;
  the DB query for "now" is skipped while the window executes. Details: skill `plsqldev-plugin-api`.
- New command = function in Core + `Sample` + tests + one line in `Commands.All`; it appears in the menu by itself.
  Skill `add-command`.
- Menu captions: ASCII only (the IDE may read them before UTF-8 mode) and no `[...]` (the IDE cuts it out).
- Analyzers (.NET latest-recommended, Meziantou, SonarAnalyzer) run as errors. Fix the code; suppress only in
  `.editorconfig` for a path with a written reason.
- Before a PR: `scripts/verify.ps1` green. Interop changes also need a check in the real IDE (skill `install-to-ide`).
- API facts come from `C:\Program Files\PLSQL Developer 14\PlugInDoc\plugindoc.pdf` and `Interface\PlugInIntf.pas`;
  never guess a callback index or signature.

## Git workflow

- `master` is protected: changes only through a PR, squash merge only, CI green (`commit-rules`, `verify`, `secrets`).
- Commit message / PR title: `feat: comment`, `opt: comment`, `feat|opt[#N]: comment` or `fix[#N]: comment` (GitHub issue number, required for fix),
  comment 1-200 chars. Branch: `feat/slug`, `opt/7-slug`, `fix/12-slug`. Rules: `scripts/commit-rules.sh`.
  Enable the hook once per clone: `git config core.hooksPath .githooks`.
- Bugs come as GitHub issues (template `bug_report.yml`); a fix starts with a failing test. Skill `fix-bug`.

## Commands

```powershell
powershell -File scripts/verify.ps1                                   # all gates (as CI)
dotnet test TextTools.slnx                                            # tests only
dotnet publish src/TextTools.Plugin -c Release                        # native DLL (needs vswhere on PATH, see verify.ps1)
dotnet run --project tools/HostEmulator -c Release -- src/TextTools.Plugin/bin/Release/net10.0/win-x64/publish/PlsqlDevTextTools.dll all
```

## Skills

- `plsqldev-plugin-api` — the PL/SQL Developer plug-in API: exports, callback indices, menu format, charset, crash safety.
- `add-command` — adding a String/Date Tools command end to end.
- `verify` — running and fixing the quality gates (analyzers, coverage, AOT, emulator).
- `install-to-ide` — installing the DLL into PL/SQL Developer and the manual check list.
- `fix-bug` — from a GitHub bug report to a `fix[#N]` PR.
- `git-workflow` — branches, commit/PR titles, squash merge, repository rules.
- `release` — versioning, GitHub release with the DLL, the Allround plug-ins page.
