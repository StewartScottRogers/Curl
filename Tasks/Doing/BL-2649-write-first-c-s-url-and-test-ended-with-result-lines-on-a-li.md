---
id: BL-2649
title: Write first.c's URL and Test ended with result lines on a lib case's stderr so upstream case 1549 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2540]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2649 — Write first.c's URL and Test ended with result lines on a lib case's stderr so upstream case 1549 passes

## Goal

A libtest case whose `<verify><stderr>` expects `first.c`'s own lines gets them: the case runner writes `URL: <argv[1]>` before the program runs and `Test ended with result <N>` after it, as curl 8.22.0's `tests/libtest/first.c` `main()` does, so upstream case 1549 passes.

## Context

Found by BL-2540, which ported `lib1549` (`Curl.Conformance.LibTest.UnitLibrary/Lib1549.cs`). Case 1549's stderr must be `URL: http://%HOSTIP:%HTTPPORT/1549`, `0 cookies`, `Test ended with result 0`; through the runner it differs at byte 0: got `0 cookies`. Read `first.c` (raw.githubusercontent.com curl/curl curl-8_22_0 tests/libtest/first.c) to see exactly when each line is written (for instance whether with no URL argument), and check that no case already on `PassingUpstreamCases.txt` starts failing because its stderr gains the lines (cases that compare stderr exactly).

## Acceptance criteria

- [ ] The case runner in `Curl.Conformance.UnitLibrary` writes `first.c`'s lines on a lib program's stderr as upstream does, with a focused unit test in `Curl.Conformance.UnitTests`.
- [ ] `CURL_UPSTREAM_CASE=1549` passes through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`, and 1549 is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] Every case on `PassingUpstreamCases.txt` still passes; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 4bcb9001 332cb953 03ca90eb.
