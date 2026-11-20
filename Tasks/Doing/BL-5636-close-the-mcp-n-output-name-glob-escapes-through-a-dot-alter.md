---
id: BL-5636
title: Close the MCP #n output-name glob escapes through a dot alternative and -K file URLs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpArgumentChecker.cs, Curl.Mcp.UnitLibrary/McpOptionValuePaths.cs, Curl.Mcp.UnitTests/McpArgumentCheckerTests.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5636 — Close the MCP #n output-name glob escapes through a dot alternative and -K file URLs

## Goal

The MCP argument checker refuses every `-o` name holding a `#<n>` glob reference that curl could
expand to a path outside `CURL_MCP_ROOT`, whether the glob comes from the tool's own arguments or
from a `-K` file, and judges each `-o` against the `--output-dir` of the `--next` group curl uses,
`-K` files included.

## Context

Found by the dark factory's shift-end review of 2026-11-20 (ADR-0710; pr-review-toolkit
code-reviewer and type-design-analyzer, verified against the code) in the shift that merged
BL-5630 (commit 92a346f416).

- `Curl.Mcp.UnitLibrary/McpOptionValuePaths.cs`, `AnyAlternativeEscapes` / `AlternativeEscapes`:
  an alternative is flagged only when it itself holds `/`, `\` or `..`. curl copies the alternative
  into the name verbatim, so a lone `.` next to a literal `.` makes `..`:
  `["-o", "#1./evil", "http://h/{.,a}"]` is allowed (judged as `_./evil`), and curl writes
  `../evil` for the first URL. `-o "#1#2/x"` with `http://h/{.}{.}` escapes the same way. On
  Windows an alternative such as `C:evil` (drive-relative) is not flagged either.
- `Curl.Mcp.UnitLibrary/McpArgumentChecker.cs`, `Check`: `AnyAlternativeEscapes(arguments)` scans
  only the top-level array. A `-K cfg` inside the root holding `url = "http://h/{../../x,y}"` with
  `-o "#1"` on the command line is allowed, and curl writes `../../x`.
- Same file: the recording walk returns `Allowed` from `CheckValue` before `CheckConfig`, and
  `CheckConfigLine` builds a throwaway `ArgumentCursor` per line, so a `next` or `output-dir` line
  in a `-K` file never moves the top-level group or its directory. Example:
  `--output-dir sub -K cfg -o ../x http://h/` with `cfg` holding `next` is judged as `sub/../x`
  (allowed) while curl's new group has no output directory and writes `../x`.

## Acceptance criteria

- [ ] `McpArgumentCheckerTests` has refused rows for `-o|#1./evil|http://h/{.,a}` and `-o|#1#2/x|http://h/{.}{.}`.
- [ ] A `-K` file holding `url = "http://h/{../../x,y}"` with `-o "#1"` on the command line is refused, pinned by a unit test.
- [ ] `--output-dir sub -K cfg -o ../x http://h/` with `cfg` holding `next` is refused, and `cfg` holding `output-dir = .` after `--output-dir a/b` judges `-o ../x` against `.`, each pinned by a unit test.
- [ ] Existing allowed rows (a `#1` with harmless alternatives such as `{a,b}`) stay allowed.
- [ ] `dotnet build` is clean and the fast tests pass; `Curl.Mcp.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: d54c9933 34cd879e 9dead49d d5a635d7 54a455d2.
