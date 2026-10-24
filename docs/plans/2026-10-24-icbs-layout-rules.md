# ICBS layout rules in Format Tools

Port of the canonical formatter post-pass (`cbs_post.post()`) into `TextTools.Core/Formatting`, one file per rule, one
setting per rule (`FormatSettings.All`, ICBS ids in `Sources`), all on by default.

- Order of the steps: `PostFormatter.Apply` (same as the reference pipeline).
- Safety: every step is guarded; a step that changes a token, a literal or a comment is dropped alone. Multi-line string
  literals stay byte-for-byte (the reference altered them in `join_bare_set` and `wrap`; fixed here).
- Verification: 213-file corpus compared with the reference output (`TEXTTOOLS_CORPUS`, `_EXPECTED`, `_OUT`); only
  difference: the reference adds a blank line when a line ends with `||` (fixed in `WrapRule`). `Cases/*.txt` are
  input/expected pairs, including the three gold examples of the standard (unchanged by the formatter).
- Known limit: one file of the corpus needs two passes (alignment vs wrapping order); the result is stable after that.
- Not ported: none of the post-pass rules; the settings import of other IDEs is still open.
