---
id: BL-1429
title: Cover HttpContentLength.TryParseItem's empty-item branch to 100% branch coverage
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1429 — Cover HttpContentLength.TryParseItem's empty-item branch to 100% branch coverage

## Goal

`HttpContentLength.TryParseItem` (Curl.Protocol.Http.UnitLibrary/HttpContentLength.cs:102) is at 100% branch coverage, so Curl.Protocol.Http.UnitLibrary is back at 100% branch.

## Context

- Found by BL-1418's `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary,Curl.Protocol.Http.UnitLibrary,Curl.Console` run on 2026-10-03: the member is at 100% line, 83.33% branch (5 of 6), the library at 99.95% branch. BL-1418 did not change the file; the gap came with BL-1387.
- Likely uncovered: the `item.Length == 0` short-circuit (an empty Content-Length list item, e.g. `Content-Length: 5,,5` or `Content-Length: ,5`); confirm with the coverage report before choosing the test.

## Acceptance criteria

- [x] A new test in Curl.Protocol.Http.UnitTests drives the uncovered branch and pins the exit code and message real curl 8.21.0 gives for that header (measure with Record-CurlExchange.ps1 if not already pinned). Met without a new test: see Notes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch for the library.

## Notes

- The uncovered branch was not the empty item. `HttpContentLengthTests.Find_ValuesCurlRefuses_ThrowExit8` has driven every empty-item case since BL-170 (`5,`, `,5`, `5,,5`, the empty value), and pins exit 8 and `Invalid Content-Length: value` as curl 8.21.0 gives them (measured in BL-170). The 6th branch was the compiler's hidden delegate-cache check for the `char.IsAsciiDigit` method group in `!item.All(char.IsAsciiDigit)`. No test can reach its other side.
- BL-1428 (commit 4df209a0, landed after this task was filed) replaced that call with `item.AsSpan().ContainsAnyExceptInRange('0', '9')`, which removed the hidden branch. A coverage run of `HttpContentLengthTests` now shows `TryParseItem` at 4/4 branches.
- 2026-10-03 21:52: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` gives Curl.Protocol.Http.UnitLibrary 100% line and 100% branch, with 0 failing members. Decision: add no test, because a new one would repeat the existing data rows. No code change was needed.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Curl.Protocol.Http.UnitLibrary measures 100% line and branch; BL-1428 removed TryParseItem's hidden delegate-cache branch, and existing tests already pin the empty-item cases
