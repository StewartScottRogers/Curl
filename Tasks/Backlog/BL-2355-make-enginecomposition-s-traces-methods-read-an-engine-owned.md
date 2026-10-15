---
id: BL-2355
title: Make EngineComposition's Traces methods read an engine-owned trace-components setting instead of CommandLineOptions
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2355 — Make EngineComposition's Traces methods read an engine-owned trace-components setting instead of CommandLineOptions

## Goal

No `Traces*` method in `Curl.Engine.UnitLibrary/EngineComposition.cs` (TracesDns, TracesSetup, TracesHaproxy, TracesSocks, TracesHttpProxy, TracesH1Proxy, TracesSsl, TracesSslProxy, TracesProxyComponent, TracesHappyEyeballs, TracesTcp, TracesTimer, TracesHttpsConnect, TracesRead, TracesMulti, TracesWrite, TracesFtp, TracesSmtp, TracesWs, TracesSsh, TracesHttp2, TracesHttp3) takes `CommandLineOptions`.

## Context

ADR-0506; one of the four pieces BL-2310 needs before the engine can drop its `Curl.Cli` reference. Measured 2026-10-15 by BL-2310: 56 reads of `options.TraceComponents` and 3 of `options.VerbosityTraceComponents` remain in the engine. `--trace-config` is curl's `curl_global_trace`, a global, not an easy option, so no `Curl.Api` easy group fits; define an engine-owned type in `Curl.Engine.UnitLibrary` (for example a record holding the trace component set and the verbosity-derived set) that `Curl.Console` builds from `CommandLineOptions` and passes in, then change every `Traces*` method and its callers to take it.

## Acceptance criteria

- [ ] No `Traces*` method in `EngineComposition.cs` names `CommandLineOptions`, and no engine source reads `TraceComponents` or `VerbosityTraceComponents` from `CommandLineOptions`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes all four gates.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
