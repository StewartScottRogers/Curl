---
id: BL-280
title: Open -w %output{file} targets on disk in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-224]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-280 — Open -w %output{file} targets on disk in Curl.Console

## Goal

`Curl.Console` supplies an `IWriteOutFileOpener` that opens `%output{file}` (truncate) and `%output{>>file}` (append) on disk and refuses, without throwing, a file it cannot open.

## Context

- Found by BL-224. The renderer in `Curl.Output.UnitLibrary` takes `IWriteOutFileOpener`; a disk implementation was left out of `Curl.Output` because its tests would touch the file system and need `TestCategory=Integration`, which BL-224 forbade.
- Open with `FileShare.ReadWrite`: curl 8.21.0 opens the same file twice in `%output{o.txt}A%output{>>o.txt}B` and the renderer flushes the first handle before opening the second. Catch `IOException`, `UnauthorizedAccessException` and `ArgumentException` (empty name) and return false.
- BL-235 composes the renderer; this can land before or with it.
- BL-235 landed: `CurlCommandRunner` takes an `IWriteOutFileOpener` (default `RefusingWriteOutFileOpener`) and builds the renderer with `writesLineFeedAsCrLf: false`, so the disk opener must wrap each opened file in `LineFeedToCrLfStream` on Windows to get the CR LF above (ADR-0040), and `CurlComposition.CreateRunner` must pass it.

## Acceptance criteria

- [ ] `%output{o3.txt}F\nG%output{>>o3.txt}H\n` through the disk opener leaves `F\r\nGH\r\n` in o3.txt on Windows, as curl 8.21.0 did (BL-224 Notes).
- [ ] An empty name, a missing directory and a directory path each return false.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; only the tests that open real files carry `TestCategory=Integration`, and `Measure-CodeQuality.ps1 -IncludeIntegration` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-26: Created.
