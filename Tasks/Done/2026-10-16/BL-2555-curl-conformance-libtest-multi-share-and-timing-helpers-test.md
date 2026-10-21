---
id: BL-2555
title: Curl.Conformance.LibTest: multi, share and timing helpers (testutil, testtrace, wait helpers)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2554]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2555 — Curl.Conformance.LibTest: multi, share and timing helpers (testutil, testtrace, wait helpers)

## Goal

C# helpers equal to `tests/libtest/testutil.c`, `testtrace.c`, `test.h`'s multi macros (`multi_init`, `multi_perform`, `multi_poll`, `multi_wait`, `multi_add_handle`, `multi_remove_handle`, `multi_timeout`, `multi_fdset`), `share_init` and `url_init` exist over `Libcurl`.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Read 	estutil.c, 	esttrace.c, 	est.h, warnless.c and Libcurl/Multi, Libcurl/Share, Libcurl/Url. Time comes from an injected TimeProvider, never a sleep.

## Acceptance criteria

- [x] Each helper has a test over a fake `ICurlEngine`; `tutil_tvnow` and `tutil_tvdiff` equivalents are tested with a manual `TimeProvider`.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- Read `first.h`, `testutil.c` and `testtrace.c` at the `curl-8_22_0` tag. 8.22.0 has no
  `test.h`, no `tutil_tvnow`/`tutil_tvdiff` (now `curlx_now`/`curlx_timediff_ms`), no `multi_wait`
  macro and no `share_init`/`url_init` macro; decisions for each are in ADR-0655.
- Added: the multi steps, `ShareInit`, `UrlInit` and `AbortOnTestTimeout` on `LibTestSteps`
  (partial files); `LibTestClock` (`TimeProvider`: `Now`, `DifferenceMilliseconds`,
  `StartTestTiming`, `WaitAsync`); `LibTestUtilities` (`SubUrl`, `ThrowawayWrite`);
  `LibTestDebugTrace` and `LibTestCliTrace` (`testtrace.c`). `tutil_rlim2str` left out (no
  `getrlimit` in managed code); `select_test` left to the first port that selects.
- Tests: the fake engine now makes a configurable `FakeMultiTransfer`, `FakeShareHandle` and
  `FakeUrlHandle`; timing runs on a hand-written `ManualTimeProvider`. 108 tests in the project.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` 100% line,
  100% branch, max complexity 8, 0 failing members (after splitting `WriteTextColumn`'s CR LF test
  out and dropping an unreachable getinfo-failure branch from `LibTestCliTrace.TryReadId`).

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: d6fde805 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-16: Doing -> Done. libtest multi/share/url steps, hang timeout on an injected TimeProvider, testutil and testtrace helpers exist over Libcurl at 100% coverage
