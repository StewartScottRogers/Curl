---
id: BL-2680
title: Interpret the %LIBTESTS precheck line so upstream case 1960 runs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2680 — Interpret the %LIBTESTS precheck line so upstream case 1960 runs

## Goal

Upstream case 1960 (`application hands over already connected socket`, run by lib1960) runs (not skipped) through the case runner and, if it passes on Windows, holds the ratchet.

## Context

Found by BL-2547, which ported lib1960 (`Curl.Conformance.LibTest.UnitLibrary/Lib1960.cs`; its URL `check` answers `CURLE_OK` with no output, upstream's "no output makes it not skipped"). The case is skipped: `test1960 is skipped: the harness does not interpret the <client><precheck> line ./libtest/libtests lib1960 check`. Teach the runner in `Curl.Conformance.UnitLibrary` to run a `%LIBTESTS libNNN <args>` precheck through `LibTestPrograms.Ported` as it runs a case's `<tool>`, skipping the case when the program prints anything or exits non-zero (runtests.pl's rule), with a unit test in `Curl.Conformance.UnitTests`. Then run case 1960 with `CURL_UPSTREAM_CASE=1960`; if it fails, file its differing lines as a task and leave it off.

## Acceptance criteria

- [x] A unit test in `Curl.Conformance.UnitTests` shows a `%LIBTESTS` precheck run through a ported program, a silent zero exit letting the case run and output or a non-zero exit skipping it.
- [x] Upstream case 1960 is run, not skipped, and is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` if it passes on Windows, or filed as a task with its differing lines if it does not.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened, and every case on the ratchet still passes.

## Notes

- 2026-10-18 (lane 2): When this run began, the runner already ran `%LIBTESTS` prechecks through the ported program. BL-2634 (test678) added `UpstreamLibTestCheckLine`, `UpstreamCaseRunner.PrecheckSkipReasonAsync`/`RunLibTestCheckAsync` and the screening of ported `%LIBTESTS` lines. Its existing tests meet criterion 1 as written:
  - `UpstreamCaseRunnerTests.RunAsync_LibTestsPrecheckThatPassesQuietly_RunsTheCase`: a silent zero exit runs the case.
  - `RunAsync_LibTestsPrecheckThatPrintsOrFails_SkipsTheCaseWithoutRunningCurl`: output, or a non-zero exit, skips it.
  - `RunAsync_LibTestsPrecheckThatThrows_SkipsWithPrecheckCommandError`.
  - `UpstreamCaseScreeningTests.FindSkipReason_LibTestsPrecheckLine_RunsOnlyWhenItsProgramIsPorted`.

  So no production code changed. The skip quoted in Context dates from before BL-2634 landed.
- Ran case 1960 with `CURL_UPSTREAM_CASE=1960` and `--filter "TestCategory=Manual"`. It runs and passes ("test1960 passes; add 1960 to PassingUpstreamCases.txt"), so 1960 is now on the ratchet. The case does not depend on the platform: it uses the in-memory sws emulation and a handed-over socket number, with no paths, files, OS error text or certificates.
- Added the ratchet file to `touches`. It never overlaps another task's (merge=union, ADR-0689).
- Fast tests: 0 failed in every project (Curl.Conformance.UnitTests: 3476 passed, 382 skipped).

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: aea2b640 7875a3fd 4cf19161.
- 2026-10-18: Doing -> Done. Upstream case 1960 runs through the existing %LIBTESTS precheck support and holds the ratchet
