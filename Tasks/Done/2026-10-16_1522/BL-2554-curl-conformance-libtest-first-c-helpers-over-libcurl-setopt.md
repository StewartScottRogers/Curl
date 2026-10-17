---
id: BL-2554
title: Curl.Conformance.LibTest: first.c helpers over Libcurl (setopt, init and the TEST_ERR codes)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2553]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2554 — Curl.Conformance.LibTest: first.c helpers over Libcurl (setopt, init and the TEST_ERR codes)

## Goal

C# helpers equal to `tests/libtest/first.c` and `test.h` exist: `test_setopt`, `easy_init`, `global_init`, `easy_perform`, `easy_getinfo`, `curl_slist_append` checks, the `TEST_ERR_*` codes with `res`-propagating semantics, and `libtest_arg2` / `libtest_arg3` / `URL` access.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Read 	ests/libtest/first.c, irst.h and 	est.h in the tarball and Libcurl as it is (Libcurl/Easy, Libcurl/Global, Libcurl/Options). Exit code values must equal upstream's TEST_ERR_* numbers.

## Acceptance criteria

- [x] Each helper has a test that drives it over a fake `ICurlEngine` and pins the `TEST_ERR_*` code it returns on each failing step.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- Read `tests/libtest/first.c` and `first.h` from the `curl-8_22_0` tag (raw.githubusercontent.com). 8.22.0 has no `test.h` and no `test_setopt`: `first.h` holds the macros and `easy_setopt` is the setopt step. Its `TEST_ERR_*` codes are `CURLE_OBSOLETE*` values (20, 24, 29, 32, 34, 40, 41, 44, 46, 50, 51, 57), not the old 115 to 126.
- Added `LibTestErrorCodes` (the twelve codes) and `LibTestSteps` (`GlobalInit`, `EasyInit`, `EasySetOption`, `EasyPerformAsync`, `EasyGetInfo`, `SlistAppend`, carrying `result` as `Result`, each answering whether the program goes on). URL and `libtest_arg2`/`_arg3` access was already `LibTestContext.Url`/`Argument2`/`Argument3` from BL-2553.
- Decision (ADR-0653): each C failure maps to the one way the Libcurl call can fail - null engine -> `CURLE_FAILED_INIT` (2), cleaned-up library -> `TEST_ERR_EASY_INIT` (29), null string -> `TEST_ERR_MAJOR_BAD` (20); setopt, perform and getinfo take the call's own code. Messages are upstream's text with caller file name and line, `\n`-terminated; perform and getinfo write nothing, as upstream does.
- Multi steps and the hang timeout are left to the first port that needs them (ADR-0653, Consequences).
- Measured: Measure-CodeQuality -Library Curl.Conformance.LibTest.UnitLibrary: 100% line, 100% branch, 0 failing members. Fast tests: all green except `Curl.Api.UnitTests` `SendAsync_CancelledWhilePaused_UnpausesAbortsAndFreesTheHandle`, which passed alone and is BL-2557's (flaky, in Doing on lane 3). `PassingUpstreamCases.txt` unchanged; Curl.Conformance.UnitTests passed (3130 passed, 0 failed).

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: e5cb0805 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-16: Doing -> Done. libtest first.h steps (global_init, easy_init, easy_setopt, perform, getinfo, slist check) and 8.22.0 TEST_ERR codes exist over Libcurl
