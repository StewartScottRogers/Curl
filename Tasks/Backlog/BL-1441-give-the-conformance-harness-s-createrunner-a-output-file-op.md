---
id: BL-1441
title: Give the conformance harness's CreateRunner a %output{} file opener and the -# progress bar
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-04
completed:
---
# BL-1441 — Give the conformance harness's CreateRunner a %output{} file opener and the -# progress bar

## Goal

Upstream cases test990, test991 and test1721 (`-w '%output{...}'`) and test1148 (`-#` with `--stderr`) pass through the conformance harness, because the `CurlComposition.CreateRunner` overload that takes an `IConnector` opens `%output{file}` targets on disk and writes the progress meter as the executable's overload does.

## Context

- Found by BL-1439 (2026-10-04). `Curl.Console/CurlComposition.cs`, the `CreateRunner(Stream, Stream, Stream, IConnector, IDatagramConnector, ...)` overload used by `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` (`RunCurlAsync`), passes no `writeOutFileOpener`, so `CurlCommandRunner` falls back to `RefusingWriteOutFileOpener` and every `%output{}` file stays unwritten; it also passes no `writesProgressMeter`, so `-#` writes no bar to the `--stderr` file. The process overload passes `new DiskWriteOutFileOpener(writesLineFeedAsCrLf: OperatingSystem.IsWindows())` and `writesProgressMeter: true`.
- Curl itself writes these files when run as the executable (checked for test790/990 in BL-1439's Context), so these failures measure the harness, not Curl.
- Mind the other callers of that overload: unit tests that expect no file writes or no progress meter may need an opt-in parameter rather than a changed default.

## Acceptance criteria

- [ ] The connector-taking `CreateRunner` can open `%output{}` files on disk and write the progress meter (by default or by an opt-in parameter the harness passes), pinned by a test in `Curl.Console.UnitTests` or the existing composition tests.
- [ ] test990, test991, test1721 and test1148 pass in `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Conformance"`, and each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; any that still fails has its remaining difference written in Notes.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-04: Created.
