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
completed:
---
# BL-1437 — Refuse a URL glob past curl's 255-piece limit with 'too many {} sets' and exit 3

## Goal

A URL glob with more pieces than curl 8.21.0 allows fails as curl's does: `curl: (3) too many {} sets in position <n>:` followed by the URL and the caret line, exit 3, instead of being expanded.

## Context

- Upstream (tag `curl-8_21_0`), `src/tool_urlglob.c` `add_glob` lines 407-425: every piece of a glob - each literal run and each `{}` set or `[]` range - is one `URLPattern`; the array starts at 2 entries (`glob_url`, line 585) and doubles, and when it must grow while `pnum` is 255 or more it fails with `globerror(glob, "too many {} sets", pos, CURLE_URL_MALFORMAT)`.
- Measured with real curl 8.21.0 (Windows, 2026-10-04): `http://testingthis/` followed by 127 copies of `{a}b` resolves the host (no error); 128 copies give `curl: (3) too many {} sets in position 403:`; 201 copies also give position 403 (upstream test 761, vendored as `Curl.Conformance.UnitTests/UpstreamTestData/test761.rawhttp`). `http://t/` + 128 × `{a}b` gives position 393; `http://testingthis/` + 128 × `[1-1]b` gives 787; `http://x/` + 128 × `b{a}` gives 394; `http://t/` + 130 × `{a}` (sets with no literal between them) does not fail. Work out how `pos` is counted from `glob_parse`/`glob_set`/`glob_range` and make Curl's position agree with every one of these measurements.
- Curl today: `Curl.Core.UnitLibrary/Globbing/UrlGlobParser.cs` has no piece limit; `UrlGlobError` already carries message, exit code and position for the existing glob errors, which `Curl.Console` prints with the URL and caret. Depends on BL-1436 because both change the parser.

## Acceptance criteria

- [ ] A data-driven test in `Curl.Core.UnitTests` pins each measured URL above: the failing ones give `too many {} sets`, `CurlExitCode.UrlMalformat` and the measured position; the 127-copy and 130-set URLs parse.
- [ ] `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Conformance"` reports `test761 passes; add 761 to PassingUpstreamCases.txt` (listing it is left to the conformance tasks), or the reason it still differs is written in Notes.
- [ ] `dotnet build Curl.Core.UnitTests -warnaserror` is clean; `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
