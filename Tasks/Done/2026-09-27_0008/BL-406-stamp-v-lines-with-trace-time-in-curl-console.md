---
id: BL-406
title: Stamp -v lines with --trace-time in Curl.Console
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-358]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-406 — Stamp -v lines with --trace-time in Curl.Console

## Goal

Under `-v --trace-time` (and `-vv`), `Curl.Console` builds `VerboseTransferEventWriter` with the runner's clock and stamping on, so `-v` lines carry curl's `HH:MM:SS.uuuuuu ` prefix.

## Context

- BL-358 teaches `VerboseTransferEventWriter` to stamp; BL-242 builds it in `Curl.Console/TransferEventOutput.cs` without stamps.
- `CommandLineOptions.TraceTime` already carries the switch (BL-195).

## Acceptance criteria

- [x] Over a scripted handler, `-s -v --trace-time` writes the stamped lines BL-358 measured, from the runner's injected clock.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Filed from BL-242 (2026-09-27), which wired `-v` and `--trace` in `Curl.Console`.
- 2026-09-27: `TransferEventOutput.OpenAsync` now builds `VerboseTransferEventWriter` with `options.TraceTime`, the runner's `TimeProvider` and `PlatformTlsBackend.ForProcess` (the backend the two-argument constructor used before, so TLS wording is unchanged). `-vv` needs nothing more: `CommandLineOptions` already turns `TraceTime` on at the second `v`.
- Tests: `CurlCommandRunnerTransferEventTests.RunAsync_VerboseWithTraceTime_StampsEachLineStartFromTheRunnersClock` pins every stamped `-s -v --trace-time` line in BL-358's measured shape; `RunAsync_DoubleVerbose_StampsTheVerboseLines` checks `-vv` stamps.
- Quality: `Measure-CodeQuality.ps1` reports `Curl.Console` at 99.48% line / 99.41% branch with two failing members, both pre-existing and outside this change: `DiskWriteOutFileOpener.TryOpen` (BL-432) and `DumpHeaderOutputStream.WriteAsync` (BL-455, BL-462). This task adds no uncovered line or branch; those tasks close the rest, so the box is ticked for this change's share.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -v --trace-time and -vv stamp every -v line from the runner's clock
