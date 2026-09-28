---
id: BL-462
title: Cover DumpHeaderOutputStream.WriteAsync's last line and DiskWriteOutFileOpener.TryOpen in the fast run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-462 — Cover DumpHeaderOutputStream.WriteAsync's last line and DiskWriteOutFileOpener.TryOpen in the fast run

## Goal

`powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member for `Curl.Console`.

## Context

- Found while finishing BL-387 (2026-09-27): the fast-run measure lists two failing `Curl.Console` members, neither changed by BL-387.
- `DumpHeaderOutputStream.WriteAsync` (`Curl.Console/DumpHeaderOutputStream.cs:85`): line 100, the `}` closing the `catch` after `throw;`, is uncovered (93.33% line, 75% branch), with or without `-IncludeIntegration`. A test whose standard-error stream completes its `WriteAsync` / `FlushAsync` asynchronously (after `Task.Yield()`) with a flush-failing destination did not cover it, so it is probably the compiler's async state machine around an `await` inside a `catch`. Restructuring the method (e.g. catching into a local and reporting after the `try`, or `ExceptionDispatchInfo`) is likely the fix; keep curl's behaviour pinned by `DumpHeaderOutputStreamTests`.
- `DiskWriteOutFileOpener.TryOpen` (`Curl.Console/DiskWriteOutFileOpener.cs:21`) is covered only by `Integration` tests, so the fast run shows it at 0%. With `-IncludeIntegration` it is covered. Either give it a seam a fast test can drive, or exclude it as a thin disk adapter the way ADR-0083 excludes the socket adapters (record which, and why).

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library Curl.Console` reports `0` failing members for `Curl.Console`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-27: Created.
