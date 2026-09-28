---
id: BL-455
title: Cover DumpHeaderOutputStream.WriteAsync's IOException path without a failure-report output
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-455 — Cover DumpHeaderOutputStream.WriteAsync's IOException path without a failure-report output

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Console` no longer lists `DumpHeaderOutputStream.WriteAsync`: its line and branch coverage are 100%.

## Context

- On 2026-09-27 (BL-361's run) the measure reported `DumpHeaderOutputStream.WriteAsync (async)` at `Curl.Console\DumpHeaderOutputStream.cs:85` with 93.33% line and 75% branch coverage, uncovered line 100: the `catch (IOException)` path is not exercised with `failureReportOutput` null (or not at all through the async overload).
- Tests live in `Curl.Console.UnitTests\DumpHeaderOutputStreamTests.cs`; a failing destination stream fake that throws `IOException` from `WriteAsync` drives it.

## Acceptance criteria

- [x] A test in `DumpHeaderOutputStreamTests` drives `WriteAsync` into its `IOException` path with and without a failure-report output, and asserts the exception is rethrown.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Console` no longer lists `DumpHeaderOutputStream.WriteAsync`.

## Notes

- 2026-09-27 (lane 1): Already satisfied before this run. Commit 151b5245 moved the failure report out of the `catch` so the compiler no longer emits an unreachable rethrow branch, and `WriteAsync_DestinationFailsTheWrite_PrintsTheFailureLineAndRethrows` and `WriteAsync_DestinationFailsWithErrorsNotShown_PrintsNothingAndRethrows` in `DumpHeaderOutputStreamTests` drive the `IOException` path with and without a failure-report output and assert the rethrow. `Measure-CodeQuality.ps1 -Library Curl.Console` on 2026-09-27 22:01: 100% line, 100% branch, 0 failing members. No code change needed.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. DumpHeaderOutputStream.WriteAsync is at 100% line and branch coverage; existing tests cover its IOException path
