---
id: BL-4500
title: Let CurlComposition's dialing runner take an injected TimeProvider so -m and --connect-timeout run on it
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlComposition.cs, Curl.Console/InProcessCurl.cs, Curl.Console.UnitTests/CurlCompositionInjectedTimeProviderTests.cs]
requirement: none
created: 2026-11-02
completed:
---
# BL-4500 — Let CurlComposition's dialing runner take an injected TimeProvider so -m and --connect-timeout run on it

## Goal

`InProcessCurl` and the dialing `CurlComposition.CreateRunner` take an optional `TimeProvider` that the run's `TransferPerformer`, `ConnectionCache`, TLS session cache and transfer timers all use, so a test can drive `-m` and `--connect-timeout` with a fake clock and get exit 28 without waiting.

## Context

Split from BL-4462 (the extended-protocol conformance harness), whose criterion "a `-m` timeout on an injected `TimeProvider` (exit 28)" needs it. Today the dialing `CreateRunner` in `Curl.Console/CurlComposition.cs` (about line 368) builds `new ConnectionCache(TimeProvider.System)` and `new TransferPerformer(TimeProvider.System, EasyErrorText)`, and `InProcessCurl.RunAsync` has no clock parameter; `signingClock` covers only signing. Add a `TimeProvider? timeProvider = null` parameter (null meaning `TimeProvider.System`) and a public `InProcessCurl.RunAsync` overload that takes it beside `readEnvironmentVariable`.

## Acceptance criteria

- [ ] The dialing `CurlComposition.CreateRunner` and a public `InProcessCurl.RunAsync` overload take an optional `TimeProvider`, used for every timer of the run in place of `TimeProvider.System`.
- [ ] A test in `Curl.Console.UnitTests/CurlCompositionInjectedTimeProviderTests.cs` runs `-m 1` against an in-memory server that never answers, advances a fake `TimeProvider`, and gets exit 28 with no real socket and no real wait.
- [ ] `dotnet build` is clean and the fast tests are green; `Curl.Console` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-02: Created.
