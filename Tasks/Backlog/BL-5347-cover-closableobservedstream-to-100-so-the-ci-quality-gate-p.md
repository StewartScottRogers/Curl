---
id: BL-5347
title: Cover ClosableObservedStream to 100% so the CI quality gate passes again
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary/McpClient/Stdio/ClosableObservedStream.cs, Curl.Conformance.Extended.UnitTests/McpClient/Stdio/ClosableObservedStreamTests.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5347 — Cover ClosableObservedStream to 100% so the CI quality gate passes again

## Goal

Every member of `ClosableObservedStream` is at 100% line and branch coverage, so CI's `Quality gates (windows-latest)` job on `work/dark-factory` is green again and the shift-end merge to `master` is not held.

## Context

CI run on `work/dark-factory` at 2026-11-17T07:51Z (the first failure after BL-5343's commit `3f0cbe7b0b`, "keep the stdio session's kill from racing the reference server's exit") failed the quality job: `Measure coverage, complexity and CRAP` reported `ClosableObservedStream.Read(byte[], int, int)`, `Flush()`, `Seek(long, SeekOrigin)`, `Write(byte[], int, int)`, `SetLength(long)`, `set_Position(long)`, `get_CanSeek()`, `get_CanRead()` (and possibly more members) as outside the line gate. BL-5343 added `Curl.Conformance.Extended.UnitLibrary/McpClient/Stdio/ClosableObservedStream.cs` (a write-only stream that records when it is closed, used by `McpStdioReferenceProcess`) without tests for it. The coordinator files failing tests and build errors, not quality-gate failures, so nothing filed this.

## Acceptance criteria

- [ ] New tests in `Curl.Conformance.Extended.UnitTests/McpClient/Stdio/ClosableObservedStreamTests.cs` cover every member of `ClosableObservedStream`: the `CanRead`/`CanSeek`/`CanWrite` values, `Length`, `Position` get and set, `Read`, `Seek` and `SetLength` throwing `NotSupportedException`, `Write` and `Flush` reaching the inner stream, and `Closed` turning true on dispose.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `ClosableObservedStream` outside a gate (if a member cannot be reached from a test, simplify the type rather than raise a threshold).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Never change `CodeMetricsConfig.txt` thresholds.

## Log

- 2026-11-17: Created.
