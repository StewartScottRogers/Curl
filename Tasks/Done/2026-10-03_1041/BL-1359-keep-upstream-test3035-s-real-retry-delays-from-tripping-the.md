---
id: BL-1359
title: Keep upstream test3035's real retry delays from tripping the conformance row's hang limit under coverage
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1359 — Keep upstream test3035's real retry delays from tripping the conformance row's hang limit under coverage

## Goal

Upstream case `test3035` never fails `UpstreamConformanceTests` by outlasting its 30-second `CaseHangLimit`, however slow the machine or the coverage collector makes it.

## Context

- On 2026-10-03, while BL-1355 measured `Curl.Conformance.UnitLibrary` with `Measure-CodeQuality.ps1`, the row `test3035` (not on `PassingUpstreamCases.txt`; normally Inconclusive in about 2.2 s) failed after 52 s with `System.TimeoutException` from `WaitAsync(CaseHangLimit)` in `UpstreamConformanceTests.cs` line 52. A rerun passed.
- The case runs `--retry 4 --retry-delay 1 --retry-all-errors`: curl waits on its own real clock between attempts, which BL-1355's wait-skipping server clock does not touch.
- A hang-limit timeout fails the row even for a case that is not listed, so an unlisted case can break the fast suite. Find what stretches the run (curl's retry waits, or a hang) and make the row independent of machine load.

## Acceptance criteria

- [x] The cause of test3035's 52-second run is named under Notes, with the file and line.
- [x] An unlisted case that outlasts the hang limit no longer fails the fast suite, or test3035 no longer takes real time beyond its retry delays.
- [x] `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

- Cause: test3035 runs `--retry 4 --retry-delay 1 --retry-all-errors`, and curl waits between attempts with `Task.Delay(wait, clock, ...)` at `Curl.Core.UnitLibrary/TransferRetrier.cs:118` on `TimeProvider.System`, the clock `CurlComposition` gives it. That is real sleeping that BL-1355's wait-skipping server clock never touches. Each wait finishes as a timer continuation on the thread pool. Under the coverage collector, with nine lanes on the machine, those continuations and the rest of the row ran late, so the row ran past the 30-second `WaitAsync(CaseHangLimit)` at `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs:52`. That threw `TimeoutException` straight out of the test, which failed the row even though the case is not listed.
- Fix (the first option in the acceptance criteria): `UpstreamConformanceTests` now catches the hang-limit `TimeoutException` and records it as `UpstreamCaseOutcome.Failed("the case did not finish within 30 seconds")`. `UpstreamCaseRatchet.Judge` then judges it like any other failure: a listed case fails its row, and an unlisted one is Inconclusive. Making curl's retry clock injectable would mean changing `Curl.Console`, which is outside `touches`. It is not needed either, because no listed case relies on retry waits.
- Verified: `dotnet build` is clean, and the fast suite is green on every test project (Conformance: 684 passed, 1805 skipped/inconclusive).

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. An unlisted upstream case that outlasts the hang limit is judged Inconclusive instead of failing the fast suite
