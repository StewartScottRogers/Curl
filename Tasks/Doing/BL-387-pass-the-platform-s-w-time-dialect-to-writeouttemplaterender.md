---
id: BL-387
title: Pass the platform's -w %time dialect to WriteOutTemplateRenderer from Curl.Console
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-290]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-387 — Pass the platform's -w %time dialect to WriteOutTemplateRenderer from Curl.Console

## Goal

On Linux and macOS, `curl -w "%time{%F %T}"` prints the date and time as the Linux curl 8.21.0 does, because `Curl.Console` builds its `WriteOutTemplateRenderer` with `WriteOutTimeDialect.Glibc` there and `WindowsCRuntime` on Windows.

## Context

- BL-290 added `WriteOutTimeDialect` and the glibc dialect in `Curl.Output.UnitLibrary` (ADR-0078). It kept the three-argument `WriteOutTemplateRenderer` constructor, which uses the Windows dialect, because `Curl.Console` was another lane's (BL-131) at the time.
- `CurlCommandRunner` builds the renderer in its `writeOutRenderer` field; `CurlComposition` already passes `OperatingSystem.IsWindows()` into the runner's other platform choices. Pass the dialect the same way (a constructor argument chosen in `CurlComposition`), so the tests choose it and cover both.
- With `Curl.Console` passing the dialect, nothing calls the three-argument constructor any more; remove it and its test so every caller has to choose.

## Acceptance criteria

- [ ] `CurlCommandRunner` takes the `WriteOutTimeDialect` it gives `WriteOutTemplateRenderer`, and `CurlComposition` passes `Glibc` when `OperatingSystem.IsWindows()` is false and `WindowsCRuntime` when it is true.
- [ ] A `Curl.Console.UnitTests` test runs `-w "%time{%F}"` through the runner with a fake `TimeProvider` and the `Glibc` dialect and gets the date, and another with `WindowsCRuntime` gets nothing.
- [ ] `WriteOutTemplateRenderer` has only the constructor that takes a `WriteOutTimeDialect`, and `Constructor_WithoutTimeDialect_RendersTheWindowsDialect` is gone.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member for `Curl.Console` or `Curl.Output.UnitLibrary`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
