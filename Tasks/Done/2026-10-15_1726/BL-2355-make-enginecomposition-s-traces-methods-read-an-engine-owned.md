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
completed: 2026-10-15
---
# BL-2355 — Make EngineComposition's Traces methods read an engine-owned trace-components setting instead of CommandLineOptions

## Goal

No `Traces*` method in `Curl.Engine.UnitLibrary/EngineComposition.cs` (TracesDns, TracesSetup, TracesHaproxy, TracesSocks, TracesHttpProxy, TracesH1Proxy, TracesSsl, TracesSslProxy, TracesProxyComponent, TracesHappyEyeballs, TracesTcp, TracesTimer, TracesHttpsConnect, TracesRead, TracesMulti, TracesWrite, TracesFtp, TracesSmtp, TracesWs, TracesSsh, TracesHttp2, TracesHttp3) takes `CommandLineOptions`.

## Context

ADR-0506; one of the four pieces BL-2310 needs before the engine can drop its `Curl.Cli` reference. Measured 2026-10-15 by BL-2310: 56 reads of `options.TraceComponents` and 3 of `options.VerbosityTraceComponents` remain in the engine. `--trace-config` is curl's `curl_global_trace`, a global, not an easy option, so no `Curl.Api` easy group fits; define an engine-owned type in `Curl.Engine.UnitLibrary` (for example a record holding the trace component set and the verbosity-derived set) that `Curl.Console` builds from `CommandLineOptions` and passes in, then change every `Traces*` method and its callers to take it.

## Acceptance criteria

- [x] No `Traces*` method in `EngineComposition.cs` names `CommandLineOptions`, and no engine source reads `TraceComponents` or `VerbosityTraceComponents` from `CommandLineOptions`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes all four gates.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

- New `EngineTraceComponents` record (`Components`, `VerbosityComponents`) in `Curl.Engine`; `Curl.Console` builds it with `EasyOptionsFromCommandLine.TraceComponentsOf`. Every `Traces*` method takes it. `CreateTcpConnector`, `CreateDohConnector` and `CreateDatagramConnectorQuicDialer` take it as a required second parameter; `CreateTransports` and `CreateDnsResolver` take an optional trailing `traces` (null is none), so the Integration tests project needed no change.
- `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console`: 0 failing members. Fast tests of Engine, Console and Conformance pass; `PassingUpstreamCases.txt` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Traces* methods take the engine-owned EngineTraceComponents
