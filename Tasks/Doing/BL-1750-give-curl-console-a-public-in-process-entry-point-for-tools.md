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
completed:
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

- [ ] `Curl.Console` has one public type with a public static method that runs a command line in process over a given `IConnector` and `IDatagramConnector` and returns curl's exit code. It has XML docs and builds the same runner as `UpstreamConformanceTests.RunCurlAsync`.
- [ ] A test in `Curl.Console.UnitTests` runs a `file://` (or other connector-free) command line through it and pins the exit code and the stdout bytes.
- [ ] `Curl.Console` stays at 100% line and branch coverage. `dotnet build` and the fast tests pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
