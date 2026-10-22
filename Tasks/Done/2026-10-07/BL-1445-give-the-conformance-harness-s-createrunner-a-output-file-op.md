---
id: BL-1445
title: Give the conformance harness's CreateRunner a %output{} file opener and the -# progress bar
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1445 — Give the conformance harness's CreateRunner a %output{} file opener and the -# progress bar

## Goal

Upstream cases test990, test991 and test1721 (`-w '%output{...}'`) and test1148 (`-#` with `--stderr`) pass through the conformance harness, because the `CurlComposition.CreateRunner` overload that takes an `IConnector` opens `%output{file}` targets on disk and writes the progress meter as the executable's overload does.

## Context

- Found by BL-1439 (2026-10-04). `Curl.Console/CurlComposition.cs`, the `CreateRunner(Stream, Stream, Stream, IConnector, IDatagramConnector, ...)` overload used by `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` (`RunCurlAsync`), passes no `writeOutFileOpener`, so `CurlCommandRunner` falls back to `RefusingWriteOutFileOpener` and every `%output{}` file stays unwritten; it also passes no `writesProgressMeter`, so `-#` writes no bar to the `--stderr` file. The process overload passes `new DiskWriteOutFileOpener(writesLineFeedAsCrLf: OperatingSystem.IsWindows())` and `writesProgressMeter: true`.
- Curl itself writes these files when run as the executable (checked for test790/990 in BL-1439's Context), so these failures measure the harness, not Curl.
- Mind the other callers of that overload: unit tests that expect no file writes or no progress meter may need an opt-in parameter rather than a changed default.

## Acceptance criteria

- [x] The connector-taking `CreateRunner` can open `%output{}` files on disk and write the progress meter (by default or by an opt-in parameter the harness passes), pinned by a test in `Curl.Console.UnitTests` or the existing composition tests.
- [x] test990, test991, test1721 and test1148 pass in `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Conformance"`, and each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; any that still fails has its remaining difference written in Notes.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Decision (sensible default): opt-in parameters, not changed defaults. The connector-taking `CurlComposition.CreateRunner` gains `writesProgressMeter = false` and `writeOutFileOpener = null`; the dozens of unit tests that pin standard error over that overload keep seeing no meter. `UpstreamConformanceTests.RunCurlAsync` passes `writesProgressMeter: true` and `new DiskWriteOutFileOpener(writesLineFeedAsCrLf: OperatingSystem.IsWindows())`, as the executable's overload does.
- Added `Curl.Console.UnitTests` to `touches` for the pinning tests the first criterion asks for (`CurlCompositionTests.CreateRunnerOverConnectors_*`); no task in Doing on origin/work/dark-factory named it.
- test990, test991, test1148 and test1721 now pass and are on `PassingUpstreamCases.txt`.
- Also seen passing but unlisted, unrelated to this change (other lanes' work): test470, test747, test760, test761. Left for whoever made them pass.
- The change adds no branch (two pass-through parameters), so Measure-CodeQuality was not run for Curl.Console; both new parameter paths are exercised by the new tests.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. The conformance harness's runner opens %output{} files and draws the -# bar; test990, 991, 1148 and 1721 pass
