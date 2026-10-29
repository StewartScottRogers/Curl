---
id: BL-4057
title: Engine easy transfer draws the built-in progress meter on CURLOPT_STDERR when progress continues, passing the Engine quality gates
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4018]
touches: [Curl.Engine.UnitLibrary/CallbackTransferProgress.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4057 — Engine easy transfer draws the built-in progress meter on CURLOPT_STDERR when progress continues, passing the Engine quality gates

## Goal

`CallbackTransferProgress` in Curl.Engine.UnitLibrary hands every progress report answered with Continue (and every report when no callback is set) to the built-in progress meter, which writes to `CURLOPT_STDERR` else the host's standard error, and every member the change touched is inside the quality gates.

## Context

Split from BL-4012 (over its cost cap). The stashed work already implements this: a lazily made `TransferProgressRecorder`, `FinishMeter` called from `ManagedEasyTransfer.Parsed.cs`, ADR-0974 and `Libcurl.UnitTests/Easy/CurlEasyProgressMeterTests.cs` (3 tests, green). `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` left three members to fix: `PerformParsedAsync` has complexity 14 (move the `FinishMeter` call into a helper); `FinishMeter` lines 119-120 are uncovered (a meter whose callback aborted, or a transfer that never started); `ReportUploaded` has 50% branch coverage (an upload report that continues to the meter). Also consider try/finally so an exception still stops the meter timer. Add the unit tests for these to Curl.Engine.UnitTests (a new file, one per case). Measure real libcurl's stderr bytes before pinning any text.

## Acceptance criteria

- [ ] Curl.Engine.UnitTests tests show a Continue answer and a missing callback reach the meter, and a callback answering 0 writes nothing.
- [ ] Curl.Engine.UnitTests tests cover `FinishMeter` for an aborted callback and a transfer that never started, and `ReportUploaded` for an upload report that continues to the meter.
- [ ] `PerformParsedAsync` has cyclomatic complexity at most 10 and an exception still stops the meter timer.
- [ ] `dotnet build` is clean and the fast tests are green.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no member this task changed outside a gate.

## Notes

- Split from BL-4012.
- Continues the stashed work of BL-4012.
- Also touches new files in Curl.Engine.UnitTests and the ADR-0974 file; name them in `touches` when claiming from Backlog is not possible to edit, so record the exact paths here before the run.

## Log

- 2026-10-29: Created.
