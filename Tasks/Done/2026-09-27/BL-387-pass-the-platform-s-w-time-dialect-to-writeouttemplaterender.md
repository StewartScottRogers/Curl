---
id: BL-387
title: Pass the platform's -w %time dialect to WriteOutTemplateRenderer from Curl.Console
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-290]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation/Planning/Decisions/ADR-0078-w-time-on-linux-and-macos-follows-glibc-strftime-in-the-c-locale.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-387 — Pass the platform's -w %time dialect to WriteOutTemplateRenderer from Curl.Console

## Goal

On Linux and macOS, `curl -w "%time{%F %T}"` prints the date and time as the Linux curl 8.21.0 does, because `Curl.Console` builds its `WriteOutTemplateRenderer` with `WriteOutTimeDialect.Glibc` there and `WindowsCRuntime` on Windows.

## Context

- BL-290 added `WriteOutTimeDialect` and the glibc dialect in `Curl.Output.UnitLibrary` (ADR-0078). It kept the three-argument `WriteOutTemplateRenderer` constructor, which uses the Windows dialect, because `Curl.Console` was another lane's (BL-131) at the time.
- `CurlCommandRunner` builds the renderer in its `writeOutRenderer` field; `CurlComposition` already passes `OperatingSystem.IsWindows()` into the runner's other platform choices. Pass the dialect the same way (a constructor argument chosen in `CurlComposition`), so the tests choose it and cover both.
- With `Curl.Console` passing the dialect, nothing calls the three-argument constructor any more; remove it and its test so every caller has to choose.

## Acceptance criteria

- [x] `CurlCommandRunner` takes the `WriteOutTimeDialect` it gives `WriteOutTemplateRenderer`, and `CurlComposition` passes `Glibc` when `OperatingSystem.IsWindows()` is false and `WindowsCRuntime` when it is true.
- [x] A `Curl.Console.UnitTests` test runs `-w "%time{%F}"` through the runner with a fake `TimeProvider` and the `Glibc` dialect and gets the date, and another with `WindowsCRuntime` gets nothing.
- [x] `WriteOutTemplateRenderer` has only the constructor that takes a `WriteOutTimeDialect`, and `Constructor_WithoutTimeDialect_RendersTheWindowsDialect` is gone.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member for `Curl.Console` or `Curl.Output.UnitLibrary`. (`Curl.Output.UnitLibrary`: 0 failing. `Curl.Console`: every member this task changed passes; two members it did not touch fail and were failing before it, filed as BL-462 - see Notes.)

## Notes

- Shape: `CurlCommandRunner` gained an optional `writeOutTimeDialect` parameter (default `WindowsCRuntime`, like its other optional collaborators, so the many test constructions stay unchanged). `CurlComposition.WriteOutTimeDialectFor(bool runsOnWindows)` picks the dialect and both `CreateRunner` overloads pass `WriteOutTimeDialectFor(OperatingSystem.IsWindows())`; a helper taking the bool keeps both branches covered on any one platform (`CurlCompositionTests.WriteOutTimeDialectFor_Platform_IsThatPlatformsCRuntime`), the pattern `CredentialEncoding.ForPlatform` uses.
- Tests: `CurlCommandRunnerWriteOutTests.RunAsync_TimeTemplateWithIsoDate_PrintsItInTheDialectTheRunnerWasGiven` runs `-w "[%time{%F}]"` at a fixed UTC 2026-09-27: `Glibc` prints `[2026-09-27]`, `WindowsCRuntime` prints `[]`. `DiskWriteOutFileOpenerTests` was the one other caller of the removed constructor; it now passes `WindowsCRuntime`.
- Touches widened: ADR-0078 and the ADR index said Console still used the Windows dialect "until BL-387"; left alone they would be false, so both were updated. Neither is in any task in Doing (BL-446 touches ADR-0093 only).
- Quality measure (`Measure-CodeQuality.ps1 -Library <name>`; a comma list in one argument matches nothing): `Curl.Output.UnitLibrary` 100/100, 0 failing. `Curl.Console` shows two failing members, neither changed here: `DiskWriteOutFileOpener.TryOpen` (only covered by Integration tests; passes with `-IncludeIntegration`) and `DumpHeaderOutputStream.WriteAsync` line 100 (the `}` after `throw;` in an async catch; a test with an asynchronously completing standard error did not cover it). Filed BL-462 for both rather than widening this task.


## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Curl.Console passes the platform's -w %time dialect: Glibc on Linux and macOS, WindowsCRuntime on Windows
