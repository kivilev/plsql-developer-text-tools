#!/bin/sh
# Single source of the commit/PR/branch naming rules; used by .githooks/commit-msg and by CI.
#
#   commit message / PR title:  <type>[#issue]: <comment, 1-200 chars>
#     type: feat | fix | opt     (opt = refactoring, tooling, CI, docs: no behavior change)
#     [#issue] is the GitHub issue number; required for fix, optional for feat/opt.
#     e.g. "fix[#12]: keep CRLF when sorting lines", "feat: add ISO week mask", "opt[#7]: split DateText"
#   branch: <type>/[issue-]slug  e.g. fix/12-crlf-sort, feat/iso-week-mask
#
# Usage: commit-rules.sh title "<first line>"   |   commit-rules.sh branch "<name>"

kind="$1"
value=$(printf '%s' "$2" | sed 's/[[:space:]]*$//')

fail() {
  echo "commit-rules: $1" >&2
  echo "  got:      $value" >&2
  echo "  expected: $2" >&2
  exit 1
}

case "$kind" in
  title)
    # Messages made by git itself, and the " (#PR)" suffix GitHub appends to squash commits.
    case "$value" in
      "Merge "*|"Revert "*|"fixup! "*|"squash! "*|"amend! "*) exit 0 ;;
    esac
    value=$(printf '%s' "$value" | sed -E 's/ \(#[0-9]+\)$//')
    prefix=$(printf '%s' "$value" | grep -oE '^(fix\[#[0-9]+\]|(feat|opt)(\[#[0-9]+\])?): ')
    [ -n "$prefix" ] || fail "bad prefix" 'feat: comment, opt: comment, feat|opt[#N]: comment, fix[#N]: comment (issue number required for fix)'
    comment=${value#"$prefix"}
    len=$(printf '%s' "$comment" | LC_ALL=C.UTF-8 wc -m | tr -d ' ')
    [ "$len" -ge 1 ] || fail "empty comment" 'a comment after the prefix'
    [ "$len" -le 200 ] || fail "comment is $len chars" 'at most 200 chars'
    ;;
  branch)
    printf '%s' "$value" | grep -qE '^(fix/[0-9]+-[a-z0-9][a-z0-9-]*|(feat|opt)/([0-9]+-)?[a-z0-9][a-z0-9-]*)$' \
      || fail "bad branch name" 'feat/slug, opt/7-slug, fix/12-slug (lower case, digits, hyphens)'
    ;;
  *)
    echo "usage: $0 title|branch <value>" >&2
    exit 2
    ;;
esac
exit 0
