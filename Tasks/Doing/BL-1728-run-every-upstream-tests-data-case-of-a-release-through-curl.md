---
id: BL-1728
title: Run every upstream tests/data case of a release through Curl with Gap/Tools/Measure-UpstreamCases.cs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1721, BL-1750]
touches: [Gap/Tools/Measure-UpstreamCases.cs, Gap/Tools/Directory.Build.props, Gap/Tools/Directory.Build.targets, Gap/Tools/Directory.Packages.props]
model: opus
requirement: none
created: 2026-10-08
completed:
---
# BL-1728 — Run every upstream tests/data case of a release through Curl with Gap/Tools/Measure-UpstreamCases.cs

## Goal

`dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <tests/data folder> <out.json>`
runs every `test*` case in a release's `tests/data` through Curl in process, with the
existing conformance harness. It writes each case's raw outcome (passed, failed with its
first difference, or skipped with the harness's reason) as JSON.

## Context

This is ADR-0433 decision 2, area `behaviour`, and decision 9 (heavy lifting in a C#
file-based app). Reuse the harness. Do not copy it. `Curl.Conformance.UnitLibrary` already
runs upstream cases in process (ADR-0013, ADR-0420). Its public entry point is
`UpstreamCaseRunner` (`Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs`), with the
constructor `(Func<UpstreamCurlInvocation, Task<int>> runCurl, UpstreamCurlPlatform
platform, TimeProvider timeProvider, TimeSpan timeLimit)` and the method
`RunAsync(int testNumber, ReadOnlyMemory<byte> testFile, string logDirectory)`, which returns
an `UpstreamCaseOutcome` (`Kind`: `Passed`, `Failed` or `Skipped`; `Detail`).

The model for wiring it is `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs`:

- its `RunCurlAsync` runs `Curl.Console` in process for one `UpstreamCurlInvocation`;
- it picks `UpstreamCurlPlatform.Windows` or `UpstreamCurlPlatform.Unix` by OS;
- it uses a 20-second `TimeLimit`, plus a 30-second hang limit around each case;
- its log folder has no blank in its path, because the runner throws on a blank. Use a
  folder under the output file's directory and check it.

Copy that wiring into the app, which reads the test files from the release cache. They are
named `test<N>` there, with no `.rawhttp` extension, because the vendored copy in
`Curl.Conformance.UnitTests/UpstreamTestData` renames them. Reference the two projects with
`#:project ../../Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary.csproj` and
`#:project ../../Curl.Console/Curl.Console.csproj`. If `RunCurlAsync` uses a type from
`Curl.Console` that is `internal` (the test project has `InternalsVisibleTo`), go through
`Curl.Console`'s public entry instead. Never add `InternalsVisibleTo` for the app: that would
change `Curl.Console`, which is outside this task's touches. If there is no public route,
stop and file a task for the smallest public seam rather than widening this one.

The repository root's `Directory.Build.props` applies to any project under the root,
file-based apps included, and its AOT and analyzer settings may break the app. If they do,
isolate `Gap/Tools/` the way `.github/gource/` is isolated, with its own
`Directory.Build.props`, `Directory.Build.targets` and `Directory.Packages.props`. Copy
those three and keep their comments' reasoning.

**Output.** One JSON file:
`{ "release": "<tests/data path>", "platform", "commit", "startedAt", "finishedAt",
"cases": [ { "number", "kind", "detail", "milliseconds" } ] }`, in test-number order. Run
cases in parallel, up to `Environment.ProcessorCount` at once (the runner supports it,
because each case has its own `%LOGDIR`), and delete each case's log folder afterwards. Give
an optional third argument, a comma-separated list of case numbers, so a run can be limited.

Known limit, to state in the app's header comment: `UpstreamCaseRunner.CurlVersion` is the
constant `"8.21.0"`, so `%VERSION` in a newer release's cases is substituted with 8.21.0.
Retargeting (BL-1748) changes it.

## Acceptance criteria

- [ ] `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <8.21.0 cache>/tests/data <temp>/raw.json` finishes on Windows and writes one entry per `test*` file. Its passed count is recorded in this task's Notes, and is not below the number of cases listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` on the same commit.
- [ ] The same command with a third argument `1,2,3` writes exactly those three cases.
- [ ] The app builds with no warnings, and building it changes no file outside `Gap/Tools/`.
- [ ] `dotnet build` of the solution and `dotnet test --filter "TestCategory!=Integration"` still pass, so the app's folder does not leak into the solution build.
- [ ] The header comment states the inputs, the output shape, the parallelism and the `%VERSION` limit.

## Notes

- 2026-10-08 (lane 3): No public route into Curl.Console exists. The in-process wiring that UpstreamConformanceTests.RunCurlAsync copies uses CurlComposition.CreateRunner and DiskWriteOutFileOpener, and both are internal. Program is internal too, and Curl.Console has no public type at all; the test project reaches them only through InternalsVisibleTo. As the Context directs, nothing was widened. BL-1750 was filed for the smallest public seam (one public static in-process run method), and this task now depends on it. Once BL-1750 is Done, wire Measure-UpstreamCases.cs through that method in place of CurlComposition.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Backlog. Waits on BL-1750: Curl.Console has no public in-process entry point, and the app may not use InternalsVisibleTo
- 2026-10-08: Backlog -> Doing.
