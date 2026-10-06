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
completed: 2026-09-27
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

- [x] `%output{o3.txt}F\nG%output{>>o3.txt}H\n` through the disk opener leaves `F\r\nGH\r\n` in o3.txt on Windows, as curl 8.21.0 did (BL-224 Notes).
- [x] An empty name, a missing directory and a directory path each return false.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; only the tests that open real files carry `TestCategory=Integration`, and `Measure-CodeQuality.ps1 -IncludeIntegration` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Delivered in the session rather than through the full `/feature` agent chain: one new class, one composition line and one constructor parameter in `Curl.Console`. No design question arose that needed an ADR; the behaviour (share for writing, text mode on Windows, refuse on `IOException`, `UnauthorizedAccessException`, `ArgumentException`) was already fixed by this task's Context and ADR-0040.
- `DiskWriteOutFileOpener(bool writesLineFeedAsCrLf)` opens `FileMode.Create` or `FileMode.Append`, `FileAccess.Write`, `FileShare.ReadWrite`; `CurlComposition.CreateRunner` (the production overload) passes it with `OperatingSystem.IsWindows()`. The fake-connector overload keeps the refusing default, so its tests write no files.
- `LineFeedToCrLfStream` gained `ownsInner` (default false): the renderer disposes each opened file, and without ownership the wrapped `FileStream` would never be closed.
- Tests: `DiskWriteOutFileOpenerTests` (the six that open real files are `TestCategory=Integration`, including the `F
GH
` case through `WriteOutTemplateRenderer`), two ownership tests in `LineFeedToCrLfStreamTests`, and `CurlCompositionTests.CreateRunner_WriteOutOutputFile_OpensItOnDisk` (Integration). `Measure-CodeQuality.ps1 -Library Curl.Console -SkipTestRun` over the `-IncludeIntegration` run: 100% line, 100% branch, 0 failing members, worst CRAP 10.
- The fast run has one unrelated failure, in `Curl.Networking.UnitTests` (`X509Chain.Build` "unknown chain building error" in the intermediate-certificate test); it fails the same way without this change touching that project. Filed as BL-354. Because of it `Measure-CodeQuality.ps1 -IncludeIntegration` stops before measuring, so the measurement above was taken from that run's Cobertura files with `-SkipTestRun`.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Curl.Console opens -w %output{file} and %output{>>file} targets on disk, CR LF on Windows, refusing ones it cannot open
