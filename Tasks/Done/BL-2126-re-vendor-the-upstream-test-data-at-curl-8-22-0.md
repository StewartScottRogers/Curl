---
id: BL-2126
title: Re-vendor the upstream test data at curl-8_22_0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2127]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2126 — Re-vendor the upstream test data at curl-8_22_0

## Goal

`Curl.Conformance.UnitTests/UpstreamTestData` holds upstream curl's `tests/data` at the
`curl-8_22_0` tag, and the upstream-case ratchet passes against it.

## Context

ADR-0479 step 3.3 (retarget to curl 8.22.0; ADR-0013 decision 3 for the vendoring). Run
`powershell -NoProfile -ExecutionPolicy Bypass -File Curl.Conformance.UnitTests\UpstreamTestData\Update-UpstreamTestData.ps1 -Tag curl-8_22_0`,
which replaces every `test*` file (as `test*.rawhttp`) and `COPYING` byte for byte. The
release diff from 8.21.0 counted 62 cases added, 89 changed and 3 removed (removed:
test1701, test323, test324; added include test5000 to test5028, test2109 to test2119,
test3223 to test3229, test1609, test1678, test1740, test1741, test1961, test1985, test2093,
test2094, test2305, test2318, test2349, test2397, test2414, test2606, test2885, test3306 and
test409).

Waits on BL-2127, which moves `UpstreamCaseRunner.CurlVersion` (the `%VERSION` the cases
expand, for example in `User-Agent: curl/%VERSION`) to 8.22.0 together with the strings Curl
prints, so the ratchet keeps matching.

`PassingUpstreamCases.txt` is a ratchet: a removed case leaves the list; a changed case that
now fails leaves it only with a `Fix` task filed for it here (a follow-up, not widened into
this task), since it is a gap against 8.22.0; added cases that pass are added. The fallout
beyond that is the gap office's to measure (BL-2128) - do not try to make the new cases pass
here. Update `Update-UpstreamTestData.ps1`'s help example to `curl-8_22_0`.

## Acceptance criteria

- [x] `UpstreamTestData` matches `tests/data` at `curl-8_22_0`: the three removed cases are gone and the added ones (for example `test5000.rawhttp`, `test3229.rawhttp`) are present.
- [x] `PassingUpstreamCases.txt` lists no removed case, and every case it dropped because it now fails has a filed task named in this task's Notes.
- [x] `Update-UpstreamTestData.ps1`'s `.PARAMETER` and `.EXAMPLE` name `curl-8_22_0`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Ran `Update-UpstreamTestData.ps1 -Tag curl-8_22_0`: 2072 test files (was 2013); 62 added, 89 changed, 3 removed (test1701, test323, test324), matching the release diff. `VendoredTestFileCount`, the data README and the project CLAUDE.md now say 8.22.0 and 2072.
- None of the three removed cases was on `PassingUpstreamCases.txt`, and no listed case fails or skips against the new data, so no case was dropped and no Fix task was needed.
- Added the new cases that pass: 1741, 2093, 2119, 3223, 3224, 3226, 3228. About 100 older cases also report "passes; add N" but were left off: listing them is outside this re-vendor (the gap office measures it, BL-2128).

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. UpstreamTestData is curl-8_22_0's tests/data and the ratchet passes against it
