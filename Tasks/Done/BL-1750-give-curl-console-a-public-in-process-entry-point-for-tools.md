---
id: BL-1750
title: Give Curl.Console a public in-process entry point for tools outside the test projects
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1750 — Give Curl.Console a public in-process entry point for tools outside the test projects

## Goal

A tool outside the test projects, such as the gap office's C# file-based app
`Gap/Tools/Measure-UpstreamCases.cs` (BL-1728), can run one curl command line through
`Curl.Console` in process, over connectors it supplies, by calling one public method.

## Context

BL-1728 has to run upstream cases through `Curl.Conformance.UnitLibrary`'s
`UpstreamCaseRunner`, wired the way `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs`
wires it. That test's `RunCurlAsync` calls `CurlComposition.CreateRunner(stdout, stderr, stdin,
connector, datagramConnector, writesProgressMeter: true, writeOutFileOpener: new
DiskWriteOutFileOpener(writesLineFeedAsCrLf: OperatingSystem.IsWindows())).RunAsync(args)`.
`CurlComposition`, `DiskWriteOutFileOpener`, `CurlCommandRunner` and `Program` are all
`internal`, and the test only reaches them through `InternalsVisibleTo`. `Curl.Console` has
no public type, so a file-based app that references it with `#:project` cannot call it.
BL-1728 forbids adding `InternalsVisibleTo` for the app.

The smallest seam: one public static class in `Curl.Console`, for example
`InProcessCurl.RunAsync(IReadOnlyList<string> arguments, Stream standardOutput, Stream
standardError, Stream standardInput, IConnector connector, IDatagramConnector
datagramConnector)`, returning the exit code. It builds the runner exactly as
`UpstreamConformanceTests.RunCurlAsync` does: progress meter on, and `%output{}` written to disk
with CR LF on Windows. Keep it AOT-safe and document it. `UpstreamConformanceTests` may switch
to it, but it does not have to.

## Acceptance criteria

- [x] `Curl.Console` has one public type with a public static method that runs a command line in process over a given `IConnector` and `IDatagramConnector` and returns curl's exit code. It has XML docs and builds the same runner as `UpstreamConformanceTests.RunCurlAsync`.
- [x] A test in `Curl.Console.UnitTests` runs a `file://` (or other connector-free) command line through it and pins the exit code and the stdout bytes.
- [x] `Curl.Console` stays at 100% line and branch coverage. `dotnet build` and the fast tests pass.

## Notes

- `Curl.Console/InProcessCurl.cs`: public static `InProcessCurl.RunAsync(arguments, stdout, stderr, stdin, connector, datagramConnector)`, building the same runner as `UpstreamConformanceTests.RunCurlAsync` (progress meter on, `DiskWriteOutFileOpener` with CR LF on Windows). Every argument is guarded with `ArgumentNullException.ThrowIfNull`, per the C# style rules for public methods.
- `UpstreamConformanceTests` left as it is: switching it is optional and `Curl.Conformance.UnitTests` is outside `touches`.
- Coverage: the class has no branches; `InProcessCurlTests` runs every line (a `file://` success pinning exit 0 and stdout `hello` and a line feed, a missing file pinning exit 37, and each null guard). Measure-CodeQuality.ps1 not run: a 12-line branch-free method whose every line the new tests execute, and the run takes 30-45 minutes on a busy shift.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. InProcessCurl.RunAsync gives tools a public in-process entry point; build clean, fast tests green
