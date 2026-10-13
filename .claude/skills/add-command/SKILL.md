---
name: add-command
description: Step-by-step recipe for adding a new menu command to Text Tools (a String Tools or Date Tools transformation, or an insert-at-cursor command) — Core function, sample for the status bar, unit and robustness tests, menu caption rules, verification. Use when the user asks for a new conversion, feature or menu item in the plugin.
---

# Adding a command

1. **Core function** in `src/TextTools.Core` (an existing class, or a new `static` class next to it):
   - Transform: `string -> string`. Insert: `DateTimeOffset -> string` (an `InsertCommand`).
   - Format with `CultureInfo.InvariantCulture`, compare with `StringComparison.Ordinal`.
   - Bad input → `throw new FormatException("<readable message>")` — it becomes the status bar text.
     Never let other exceptions out; convert overflow/out-of-range (multi-line date-like commands: use `DateText.PerLine`,
     it already converts and keeps indentation and trailing commas).
   - Regex: `[GeneratedRegex(..., RegexOptions.ExplicitCapture, Limits.RegexTimeoutMs)]`, named groups only.
2. **Register** one line in `Commands.All` (`src/TextTools.Core/Commands.cs`), in the right `Menu` and `Group`:
   `T(StringMenu, "SQL", "Caption", SqlText.Fn, "sample input")`.
   - Caption: ASCII only, no `[`/`]`, short, says input -> output (`"Lines -> IN (...)"`).
   - Sample: a short single-line input; the status bar shows `Caption  (e.g. sample -> result)`. Give one for every
     single-line command; skip it for multi-line commands (Lines group, IN list).
   - Total menu indices must stay ≤ 99 (test `FitsIntoNinetyNineMenuIndices`).
3. **Tests** in `tests/TextTools.Core.Tests`:
   - Behavior: `[Theory]` with typical inputs, edge cases (empty, CRLF, Cyrillic, trailing newline), and rejects
     (`Assert.Throws<FormatException>`).
   - Robustness is automatic: `RobustnessTests` fuzzes every command in `Commands.All`. If it fails, fix the function,
     do not exclude the command.
   - Coverage gate: 95% lines of Core.
4. **Verify**: `powershell -File scripts/verify.ps1` (skill `verify`). For commands that touch interop or the status bar,
   add a check to `tools/HostEmulator/Scenarios.cs` (`Smoke`).
5. **Docs**: README feature list; CLAUDE.md overview if a new group appears.
6. **Commit / PR**: branch `feat/<slug>` (or `feat/<issue>-<slug>`), title `feat[#N] add <command>` (skill `git-workflow`).
