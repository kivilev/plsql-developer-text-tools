# Idea: Format Tools (Beautifier + own post-pass)

Status: idea only, nothing implemented. Origin: team code-style work (ICBS coding standard), 2026-10-22.

## Goal
A menu command that runs the IDE's own PL/SQL Beautifier and then a configurable post-pass written in this plug-in,
so the editor text reaches the team's formatting standard without extra tools (one DLL, no exe, no service, no Python).

## Why it fits here
- Native AOT DLL already exists; Core has a PL/SQL lexer (`Sql/PlsqlLexer.cs`) that is safer than regexes for strings, comments, parentheses.
- `IDE_BeautifyText(S)` (index 162, since v510) and `IDE_BeautifyWindow` (161) are official API. `IDE_BeautifierOptions` (160) tells whether the IDE
  beautifies on save/load/compile. The Beautifier uses the rules file set in the IDE preferences; a rules file cannot be passed per call.
- Events: `OnFileSaved` / `OnFileLoaded` (v514, after the fact only), `BeforeExecuteWindow` (v714, may cancel), `CommandLine` (v513) for
  Command Window batch. No before-save hook; format-on-save would mean save, rewrite, save again (guard against loops) - not recommended.

## Sketch
1. `IDE_BeautifyText` callback in `Ide.cs`.
2. Core `PlsqlFormatter`: post-pass on lexer tokens; rules are options (settings page / JSON next to the rules file), not hard-coded.
3. Command `Format (post-pass)`: selection or whole window: Beautifier, then post-pass, then `IDE_SetText` (keep cursor, check `Changed`/read-only, never rewrite
   text with U+FFFD or unconfirmed non-ASCII, as for other commands).
4. Optional `CommandLine` batch over a folder (check whether it needs a DB connection).

## Candidate rules (measured on 213 ICBS files: Beautifier alone vs Beautifier + a Python prototype of the post-pass)
- Already closed by the prototype: `END <name>;` (233 -> 0), `:=` alignment groups (6 -> 0), single space in `IN VARCHAR2`.
- Prototype breaks `=>` alignment on Beautifier input (0 -> 33): fix when porting.
- Not covered, need new code: one select column per line (287 of 481 queries), right-aligned `FROM`/`WHERE`/`ORDER BY` per query block
  (61 of 728), INSERT layout (3 of 29), lowercase keywords in subqueries, line wrapping (> 76 / > 120).
- Realistic ceiling: ~12-14 of 16 measurable rules; a headless formatter (SQLcl + Arbori + Python) reached 15 of 16 and stays the source of truth in CI.

## Open points
- Rules are team-specific: keep the formatter generic and the rule set in a config file (the team's lives in its own repo), do not hard-code a standard.
- Keep golden tests: same input must give the same output as any reference implementation the team uses in CI.
- Distribution of an unsigned native DLL in a bank (AV/AppLocker, security review).
- Legal: v15 `License.rtf` has no reverse-engineering text; not needed anyway, the Beautifier is called through the API.
