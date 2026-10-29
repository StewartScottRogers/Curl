---
id: BL-4057
title: Engine easy transfer draws the built-in progress meter on CURLOPT_STDERR when progress continues, passing the Engine quality gates
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4018]
touches: [Curl.Engine.UnitLibrary/CallbackTransferProgress.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs, Curl.Engine.UnitTests/CallbackTransferProgressTests.cs, Curl.Engine.UnitTests/CallbackTransferProgressMeterTests.cs, Libcurl.UnitTests/Easy/CurlEasyProgressMeterTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4057 — Engine easy transfer draws the built-in progress meter on CURLOPT_STDERR when progress continues, passing the Engine quality gates

## Goal

`CallbackTransferProgress` in Curl.Engine.UnitLibrary hands every progress report answered with Continue (and every report when no callback is set) to the built-in progress meter, which writes to `CURLOPT_STDERR` else the host's standard error, and every member the change touched is inside the quality gates.

## Context

Split from BL-4012 (over its cost cap). The stashed work already implements this: a lazily made `TransferProgressRecorder`, `FinishMeter` called from `ManagedEasyTransfer.Parsed.cs`, ADR-0974 and `Libcurl.UnitTests/Easy/CurlEasyProgressMeterTests.cs` (3 tests, green). `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` left three members to fix: `PerformParsedAsync` has complexity 14 (move the `FinishMeter` call into a helper); `FinishMeter` lines 119-120 are uncovered (a meter whose callback aborted, or a transfer that never started); `ReportUploaded` has 50% branch coverage (an upload report that continues to the meter). Also consider try/finally so an exception still stops the meter timer. Add the unit tests for these to Curl.Engine.UnitTests (a new file, one per case). Measure real libcurl's stderr bytes before pinning any text.

## Acceptance criteria

- [x] Curl.Engine.UnitTests tests show a Continue answer and a missing callback reach the meter, and a callback answering 0 writes nothing.
- [x] Curl.Engine.UnitTests tests cover `FinishMeter` for an aborted callback and a transfer that never started, and `ReportUploaded` for an upload report that continues to the meter.
- [x] `PerformParsedAsync` has cyclomatic complexity at most 10 and an exception still stops the meter timer.
- [x] `dotnet build` is clean and the fast tests are green.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no member this task changed outside a gate.

## Notes

- Split from BL-4012.
- Continues the stashed work of BL-4012.
- Also touches new files in Curl.Engine.UnitTests and the ADR file (now ADR-0993); name them in `touches` when claiming from Backlog is not possible to edit, so record the exact paths here before the run.
- Added the three test files to `touches` (the stashed BL-4012 work changed `CallbackTransferProgressTests.cs` for the new `Of` signature and added `CurlEasyProgressMeterTests.cs`; this run added `CallbackTransferProgressMeterTests.cs`); no task in Doing on origin/work/dark-factory names any of them.
- `PerformParsedAsync` now calls `FollowFinishingMeterAsync`, which wraps `FollowUnlessAbortedAsync` and calls `FinishMeter` in a `finally`, so an exception still stops the meter's timer and the perform's complexity is back under 10.
- The new tests sit in their own file, `CallbackTransferProgressMeterTests`, as the task asked, beside the existing `CallbackTransferProgressTests`. The upload line they pin is the one the existing `TransferProgressRecorder` draws (its format was measured against curl 8.21.0 under BL-131); no new text was measured, since a lane has no C toolchain to build a libcurl program.
- The stashed ADR was ADR-0974, which another lane already uses; renumbered this lane's to ADR-0993 (file, heading, README row, citations).
- `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members. Solution build clean; fast tests green on Windows. The tests use only in-memory streams and a manual clock, so they do not depend on the platform (criterion on CI met by that).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 2aa3d39e 58ff91c0 82a40b63 e4d345af 0b861f72.
- 2026-10-29: Doing -> Done. Engine easy transfer draws the built-in meter; FinishMeter in a finally; Engine quality gates pass
