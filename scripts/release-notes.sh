#!/bin/sh
# Drafts a CHANGELOG.md section from the squash commits since a tag, grouped by commit type.
#   sh scripts/release-notes.sh v0.1.0 0.2.0      (previous tag, new version)
#   sh scripts/release-notes.sh - 0.1.0           (no previous tag: whole history)
# Paste the output at the top of CHANGELOG.md in the "opt release X.Y.Z" PR and edit it into plain words.

prev="$1"; version="$2"
[ -n "$version" ] || { echo "usage: $0 <previous tag|-> <new version>" >&2; exit 2; }
repo="https://github.com/kivilev/plsql-developer-text-tools"
range="HEAD"; [ "$prev" != "-" ] && range="$prev..HEAD"

subjects=$(git log --format=%s "$range")

# "fix[#12] keep CRLF (#34)" -> "- keep CRLF ([#12](.../issues/12)) ([PR #34](.../pull/34))"
section() {
  title="$1"; type="$2"
  items=$(printf '%s\n' "$subjects" | grep -E "^$type(\[#[0-9]+\])? " | sed -E \
    -e "s|^$type\[#([0-9]+)\] (.*)$|- \2 ([#\1]($repo/issues/\1))|" \
    -e "s|^$type (.*)$|- \1|" \
    -e "s| \(#([0-9]+)\)( \(\[#[0-9]+\]\(.*\)\))?$|\2 ([PR #\1]($repo/pull/\1))|")
  [ -n "$items" ] && printf '### %s\n\n%s\n\n' "$title" "$items"
}

printf '## [v%s] - %s\n\n' "$version" "$(date +%Y-%m-%d)"
section "Features" feat
section "Fixes" fix
section "Internal" opt
