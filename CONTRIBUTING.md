# Contributing

Thanks for helping with Text Tools for PL/SQL Developer.

## Questions, bugs, ideas

- **Question** ("how do I…", "is it possible…"): [Discussions → Q&A](https://github.com/kivilev/plsql-developer-text-tools/discussions).
- **Bug**: an [issue with the bug form](https://github.com/kivilev/plsql-developer-text-tools/issues/new/choose).
  The exact selected text, the command and the plugin / IDE versions make it reproducible.
- **New command**: a feature request with an input → output example, or an idea in Discussions.
- **Security problem**: never in a public issue, see [SECURITY.md](SECURITY.md).

## Build

Requirements: Windows x64, .NET 10 SDK, Visual Studio 2022 Build Tools with the C++ workload (Native AOT linker).

```powershell
git config core.hooksPath .githooks                      # commit message check, once per clone
powershell -File scripts/verify.ps1                      # every gate CI runs
```

The DLL: `src/TextTools.Plugin/bin/Release/net10.0/win-x64/publish/PlsqlDevTextTools.dll`. To try it, copy it into
`<PL/SQL Developer>\PlugIns` with the IDE closed.

## Pull requests

1. Branch from `master`: `feat/<slug>`, `opt/<slug>`, `fix/<issue>-<slug>`.
2. Commit message and PR title: `feat <comment>`, `opt <comment>`, `fix[#<issue>] <comment>` (a fix needs an issue),
   1–200 chars, English, imperative: `fix[#12] keep CRLF when sorting lines`. Rules: [scripts/commit-rules.sh](scripts/commit-rules.sh).
3. `scripts/verify.ps1` green: format, analyzers as errors, tests with the coverage gate, Native AOT publish, host emulator.
4. A bug fix starts with a test that reproduces it. A new command needs unit tests and a status bar sample
   (see `.claude/skills/add-command/SKILL.md`).
5. PRs are squash-merged: the PR title becomes the commit on `master`.

Rules that are not negotiable: the plugin must never crash the IDE or corrupt the user's text
(details in [CLAUDE.md](CLAUDE.md#rules)); menu captions are ASCII without `[...]`.

## Versions

[Semantic Versioning](https://semver.org/). Menu captions are public API (users bind hotkeys to them):
renaming or removing a command is a major change. See [CHANGELOG.md](CHANGELOG.md).

## Claude Code

The repository is set up for [Claude Code](https://claude.com/claude-code): project rules in `CLAUDE.md`,
development skills (add a command, verify, fix a bug, release, the PL/SQL Developer plug-in API) in `.claude/skills/`.
