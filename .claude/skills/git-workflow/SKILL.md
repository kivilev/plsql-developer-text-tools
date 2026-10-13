---
name: git-workflow
description: Git and GitHub rules of Text Tools — branch names, commit message and PR title format (feat|fix|opt[#issue]), squash-only merges into protected master, required CI checks, and how the repository settings are configured. Use when committing, creating a branch or PR, writing a commit message, or changing repository settings.
---

# Git workflow

Rules live in `scripts/commit-rules.sh` (used by the local hook and by CI). Enable the hook once per clone:
`git config core.hooksPath .githooks`.

## Names

| What | Format | Examples |
|---|---|---|
| Commit message / PR title | `feat\|opt[#N] comment`, `fix[#N] comment` | `feat add ISO week mask`, `feat[#3] add JSON pretty print`, `fix[#12] keep CRLF when sorting lines`, `opt[#7] split DateText` |
| Branch | `feat/slug`, `opt/[N-]slug`, `fix/N-slug` | `feat/iso-week-mask`, `fix/12-crlf-sort` |

- `feat` new user-visible behavior; `fix` a bug (GitHub issue required); `opt` everything without behavior change:
  refactoring, tests, CI, docs, dependencies.
- `[#N]` is a GitHub issue (there is no Jira). Comment 1-200 chars, lower case start, imperative, English.
- GitHub appends ` (#PR)` to squash commits; the rules accept it.

## Flow

1. `git switch -c <branch> origin/master`, commit (hook checks the message), `scripts/verify.ps1` green.
2. `git push -u origin <branch>`, `gh pr create` — the title must follow the format (it becomes the commit on master).
   Body: what/why, `Fixes #N` for bugs, checklist from the template.
3. CI: `commit-rules`, `verify`, `secrets` must pass. Squash merge (the only allowed method); the branch is deleted.
4. Never push to `master` directly, never force-push it, never merge with failing checks.

## Dependabot PRs

Dependabot titles come as `opt: Bump x from 1 to 2` (it adds a colon after the prefix), which breaks the format.
`commit-rules` is skipped for Dependabot, so fix the title when merging:
`gh pr edit N --title "opt bump x from 1 to 2"` and `gh pr merge N --squash --subject "opt bump x from 1 to 2"`.
A PR created before a CI fix on master: comment `@dependabot rebase`. Several Actions PRs touch `ci.yml`: merge one,
let Dependabot rebase the rest, then the next.

## Repository settings

- Applied: merge squash only (`allow_merge_commit=false`, `allow_rebase_merge=false`), squash title = PR title,
  squash message = PR body, delete branch on merge, suggest updating branches.
- Pending (see below): `master` ruleset — PR required, required checks `commit-rules`, `verify`, `secrets`
  up to date with master, linear history, no force-push, no deletion, conversations resolved, applies to admins.
  No required approvals (single maintainer; raise to 1 when a second maintainer joins).
- Applied: Dependabot NuGet and GitHub Actions weekly (`opt` prefix), security alerts and updates on.
- The `master` ruleset is stored in `.github/rulesets/master-protection.json`. Apply it with
  `gh api -X POST repos/kivilev/plsql-developer-text-tools/rulesets --input .github/rulesets/master-protection.json`.
  A private repository on the free GitHub plan rejects it (HTTP 403 "Upgrade to GitHub Pro or make this repository
  public"): apply it right after the repository becomes public (or on GitHub Pro). Until then the rules are kept by
  discipline: never push to master, always a PR with green CI, squash merge (squash-only is enforced by repo settings).
