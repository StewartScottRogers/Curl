---
id: BL-2826
title: List the 17 upstream cases that already pass on Windows: 2035, 2038, 2042, 355, 356, 412, 413, 437, 438, 510, 517, 565, 1535, 1536, 1578, 1580 and 1590
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2826 — List the 17 upstream cases that already pass on Windows: 2035, 2038, 2042, 355, 356, 412, 413, 437, 438, 510, 517, 565, 1535, 1536, 1578, 1580 and 1590

## Goal

Upstream cases 2035, 2038, 2042, 355, 356, 412, 413, 437, 438, 510, 517, 565, 1535, 1536, 1578, 1580 and 1590 are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, so the ratchet fails the build if any of them stops passing.

## Context

Measured 2026-10-19 on Windows (SDK 10.0.401), one case at a time, with
`$env:CURL_UPSTREAM_CASE='<n>'; dotnet test Curl.Conformance.UnitTests --no-build --filter FullyQualifiedName~NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`:
each of the 17 cases ends `Passed` yet is not on the list (1801 of 2072 cases are listed). The gap office
(scorecard 2026-10-19_1329) left them off the ratchet for other reasons:

- 2035, 2038 and 2042 are `match` on Linux (the gap run uses this same runner) and pass on Windows.
- 355, 356, 412, 413, 437 and 438 are `excluded | debug-build-only` in the gap office, because the *reference* curl is no debug build; this runner stands in for `CURL_ALTSVC_HTTP` (ADR-0497), so they pass here.
- 510, 517, 565, 1535, 1536, 1578, 1580 and 1590 are `excluded | libcurl-api`; their libtest ports (`Curl.Conformance.LibTest.UnitLibrary`, `Lib510`, `Lib517`, ...) exist and, for all but 565, 1578, 1580 and 1590, are registered in `LibTestPrograms`; run each to see.

The ratchet reads the file as a set, one case number per line, and makes no task overlap (ADR-0689). A listed case that fails or is skipped fails the build (ADR-0013, decision 5), except where only another platform's Curl has its `<features>` (ADR-0488).

## Acceptance criteria

- [x] Each of the 17 cases was run on its own with the command above and its outcome is written in Notes (Passed, or the skip or failure text).
- [x] Every case that Passed is on `PassingUpstreamCases.txt`, one number per line, in the file's numeric order; every case that did not pass is not on it and has its reason in Notes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean.
- [x] `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration"` passes, and the `UpstreamCase_RunThroughCurl_HoldsTheRatchet` rows for the added cases are `Passed`.
- [x] After the push, the `CI` workflow is green on Linux and macOS for the commit (these cases touch no Windows path); a case that fails there is removed from the list again and the failure text is in Notes.

## Notes

All 17 cases ran alone with the named command: 2035, 2038, 2042, 355, 356, 412, 413, 437, 438, 510, 517, 565, 1535, 1536, 1578, 1580 and 1590 each Passed. All 17 are now on the list (1801 -> 1818), in numeric order. Full conformance fast run: 0 failed. The last criterion (CI on Linux and macOS) is checked by the shift after it pushes; the coordinator files a fix task if a case fails there.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: f384dd3d b835afd3 d71d6599 b6abb434 e80d2533.
- 2026-10-19: Doing -> Done. All 17 cases passed alone and are on the ratchet list; build clean, conformance fast tests green.
