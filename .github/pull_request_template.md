<!--
PR title = the squash commit message on master:  feat: comment, opt: comment, feat|opt[#N]: comment  or  fix[#N]: comment  (issue number required for fix)
Branch: feat/slug, opt/7-slug, fix/12-slug. Rules: scripts/commit-rules.sh. CI fails on a wrong title or branch.
-->

## What and why

Fixes #

<!-- 1-3 lines. -->

## Checklist

- [ ] `scripts/verify.ps1` passes locally (format, analyzers, tests, coverage, AOT publish, host emulator)
- [ ] New or changed behavior is covered by unit tests; a bug fix starts with a test that reproduces it
- [ ] Menu captions: ASCII only, no `[...]`
- [ ] Interop changed (`src/TextTools.Plugin`)? Checked in a real PL/SQL Developer: menu, the command, Ctrl+Z
- [ ] README feature list / CLAUDE.md updated if the user-visible behavior or the rules changed
