---
id: BL-453
title: Cover DumpHeaderOutputStream.WriteAsync's IOException path without a failure-report output
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-453 — Cover DumpHeaderOutputStream.WriteAsync's IOException path without a failure-report output

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Console` no longer lists `DumpHeaderOutputStream.WriteAsync`: its line and branch coverage are 100%.

## Context

- On 2026-09-27 (BL-361's run) the measure reported `DumpHeaderOutputStream.WriteAsync (async)` at `Curl.Console\DumpHeaderOutputStream.cs:85` with 93.33% line and 75% branch coverage, uncovered line 100: the `catch (IOException)` path is not exercised with `failureReportOutput` null (or not at all through the async overload).
- Tests live in `Curl.Console.UnitTests\DumpHeaderOutputStreamTests.cs`; a failing destination stream fake that throws `IOException` from `WriteAsync` drives it.

## Acceptance criteria

- [ ] A test in `DumpHeaderOutputStreamTests` drives `WriteAsync` into its `IOException` path with and without a failure-report output, and asserts the exception is rethrown.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Console` no longer lists `DumpHeaderOutputStream.WriteAsync`.

## Notes

## Log

- 2026-09-27: Created.
