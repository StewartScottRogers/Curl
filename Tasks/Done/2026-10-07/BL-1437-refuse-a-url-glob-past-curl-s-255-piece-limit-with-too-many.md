---
id: BL-1437
title: Refuse a URL glob past curl's 255-piece limit with 'too many {} sets' and exit 3
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1436]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-04
---
# BL-1437 — Refuse a URL glob past curl's 255-piece limit with 'too many {} sets' and exit 3

## Goal

A URL glob with more pieces than curl 8.21.0 allows fails as curl's does: `curl: (3) too many {} sets in position <n>:` followed by the URL and the caret line, exit 3, instead of being expanded.

## Context

- Upstream (tag `curl-8_21_0`), `src/tool_urlglob.c` `add_glob` lines 407-425: every piece of a glob - each literal run and each `{}` set or `[]` range - is one `URLPattern`; the array starts at 2 entries (`glob_url`, line 585) and doubles, and when it must grow while `pnum` is 255 or more it fails with `globerror(glob, "too many {} sets", pos, CURLE_URL_MALFORMAT)`.
- Measured with real curl 8.21.0 (Windows, 2026-10-04): `http://testingthis/` followed by 127 copies of `{a}b` resolves the host (no error); 128 copies give `curl: (3) too many {} sets in position 403:`; 201 copies also give position 403 (upstream test 761, vendored as `Curl.Conformance.UnitTests/UpstreamTestData/test761.rawhttp`). `http://t/` + 128 × `{a}b` gives position 393; `http://testingthis/` + 128 × `[1-1]b` gives 787; `http://x/` + 128 × `b{a}` gives 394; `http://t/` + 130 × `{a}` (sets with no literal between them) does not fail. Work out how `pos` is counted from `glob_parse`/`glob_set`/`glob_range` and make Curl's position agree with every one of these measurements.
- Curl today: `Curl.Core.UnitLibrary/Globbing/UrlGlobParser.cs` has no piece limit; `UrlGlobError` already carries message, exit code and position for the existing glob errors, which `Curl.Console` prints with the URL and caret. Depends on BL-1436 because both change the parser.

## Acceptance criteria

- [x] A data-driven test in `Curl.Core.UnitTests` pins each measured URL above: the failing ones give `too many {} sets`, `CurlExitCode.UrlMalformat` and the measured position; the 127-copy and 130-set URLs parse.
- [x] `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Conformance"` reports `test761 passes; add 761 to PassingUpstreamCases.txt` (listing it is left to the conformance tasks), or the reason it still differs is written in Notes.
- [x] `dotnet build Curl.Core.UnitTests -warnaserror` is clean; `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports no failing member.

## Notes

- Rule derived from the measurements: the glob fails when its 256th piece (literal run, set or range) has been read, at the column just past that piece, counted as every other glob error is (index + 1, minus one per closed set). This matches all five measured positions (403, 403, 393, 787, 394), and 255 pieces (127 x `{a}b` after a prefix) or 131 pieces (130 x `{a}`) parse. Implemented as `UrlGlobParser.RefuseTooManyPieces` after each piece.
- test761 also needed curl's message buffer: curl formats a glob error into 512 bytes, so the message is cut to 511 characters, which drops the URL's tail and the caret line (`UrlGlobError.ToMessage`). The cut counts UTF-16 characters, not UTF-8 bytes; the two differ only for a non-ASCII URL past 511 bytes, which no measurement covers.
- `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Conformance"` now reports `test761 passes; add 761 to PassingUpstreamCases.txt`; listing it is left to the conformance tasks.
- Measure-CodeQuality -Library Curl.Core.UnitLibrary: 0 failing members.

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
- 2026-10-04: Doing -> Done. A URL glob's 256th piece fails as curl's does: 'too many {} sets in position N', exit 3, message cut to 511 characters; test761 passes
