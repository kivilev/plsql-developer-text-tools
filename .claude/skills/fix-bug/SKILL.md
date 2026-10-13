---
name: fix-bug
description: Takes a GitHub bug report for Text Tools from issue to merged fix — read the issue, reproduce it with a failing unit test or emulator scenario, fix, verify, open a fix[#N] PR that closes the issue. Use when the user mentions a bug, an issue number, a crash, wrong output, or pastes a bug report.
---

# Fixing a bug

1. **Read the issue**: `gh issue view <N> --comments`. Take plugin/IDE versions, the command, the exact input,
   expected and actual results. Missing input → ask in the issue (`gh issue comment`) before guessing.
2. **Branch**: `git switch -c fix/<N>-<slug> origin/master`.
3. **Reproduce first**:
   - Wrong text output → a failing `[Fact]`/`[Theory]` in `tests/TextTools.Core.Tests` with the reporter's input.
   - Crash, dialog, encoding, menu, status bar → a scenario in `tools/HostEmulator/Scenarios.cs`
     (or `tests/TextTools.Plugin.Tests` for the menu). See skill `plsqldev-plugin-api` for the crash-safety rules.
   - Only the real IDE shows it (undo, cursor with tabs) → reproduce there (skill `install-to-ide`) and write
     down the steps in the PR.
   Confirm the test fails for the reported reason.
4. **Fix** the cause, not the symptom. If the same class of input can break other commands, extend
   `RobustnessTests.Nasty` with it.
5. **Verify**: `scripts/verify.ps1` (skill `verify`).
6. **Commit and PR**: commit `fix[#N] <what was wrong, 1-200 chars>`; PR title the same, body `Fixes #N`
   (closes the issue on squash merge). `gh pr create --fill` then edit the title if needed.
7. After merge: comment in the issue which version will contain the fix.
