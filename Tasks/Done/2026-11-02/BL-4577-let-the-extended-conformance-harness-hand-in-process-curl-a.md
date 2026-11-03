---
id: BL-4577
title: Let the extended conformance harness hand in-process curl a stand-in extension process starter
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/InProcessCurl.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/InProcessCurlExtensionProcessTests.cs, Curl.Conformance.Extended.UnitLibrary/Harness/ExtendedConformanceHarness.cs, Curl.Conformance.Extended.UnitLibrary/Harness/InProcessCurlRunner.cs, Curl.Conformance.Extended.UnitLibrary/Harness/InProcessCurlProcessRunner.cs, Curl.Conformance.Extended.UnitTests/Harness/ExtendedConformanceHarnessProcessTests.cs, Documentation/Conformance/Extended/README.md]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4577 — Let the extended conformance harness hand in-process curl a stand-in extension process starter

## Goal

A conformance case can run a real curl command line in process with a stand-in `IExtensionProcessStarter`, so the
`mcp+stdio://` (BL-4483) and `curl-proto-<scheme>` Plugin (BL-4489) cases start no real process.

## Context

BL-4463 (ADR-1184) gave `EngineComposition.CreateProtocolHandlers` an `extensionProcessStarter` parameter that
reaches `ProtocolHandlerServices.ProcessStarter`, but nothing above the engine passes one: `Curl.Console`'s
`InProcessCurl.RunAsync` overloads and `CurlComposition`'s dialing runner (the `CreateProtocolHandlers` calls in
`Curl.Console/CurlComposition.cs`, around lines 559 and 950) never take or pass a starter, and the conformance
harness's `InProcessCurlRunner` delegate and `ExtendedConformanceHarness.RunAsync` have no parameter for one. So
BL-4483 and BL-4489, which may add nothing to `Harness/`, cannot run curl against a stand-in process (found by
BL-4483's first run, 2026-11-02, before any edit).

Suggested shape (decide and record): a new public `InProcessCurl.RunAsync` overload taking an
`IExtensionProcessStarter` beside the clock, passed through the dialing runner to `CreateProtocolHandlers`; in the
harness, an optional `IExtensionProcessStarter? processStarter` on `ExtendedConformanceHarness.RunAsync` with a
runner delegate that carries it (a new delegate type in its own file, or the existing delegate widened), keeping
every existing case file compiling unchanged. When a stand-in is given, the program it reads `PATH` from is the
stand-in's `ReadEnvironmentVariable`, as ADR-1184 says.

## Acceptance criteria

- [x] `InProcessCurl` has a public overload that takes an `IExtensionProcessStarter`, and a test in
      `Curl.Console.UnitTests` shows an `mcp+stdio://localhost/server?a` run through it starts `server` with `a`
      on the stand-in and starts no real process.
- [x] `ExtendedConformanceHarness.RunAsync` accepts an optional process starter and hands it to curl; a test in
      `Curl.Conformance.Extended.UnitTests/Harness/` shows it reaches the stand-in; every existing case file
      compiles unchanged.
- [x] `Documentation/Conformance/Extended/README.md` says how a case passes a stand-in process starter.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; `Curl.Console` and the harness stay at 100% line and branch coverage, complexity at most 10 and CRAP
      at most 30 per method; no package added; tests platform-neutral.

## Notes
- Decided (ADR-1203): a new public `InProcessCurl.RunAsync` overload takes an `IExtensionProcessStarter` after the
  clock and reaches `CreateProtocolHandlers` through a new optional `extensionProcessStarter` on
  `CurlComposition.CreateRunner` and `CreateDialingTransferDispatch`. I left the production `CreateTransferDispatch`
  (line 950) alone, because the shipped curl keeps each extension's real starter. In the harness, a new delegate
  `InProcessCurlProcessRunner` and an `ExtendedConformanceHarness.RunAsync` overload with the starter fourth make the
  starter optional by overload choice, so every existing case file compiles unchanged. Both overloads share one
  private body.
- Tests: `InProcessCurlExtensionProcessTests` and `ExtendedConformanceHarnessProcessTests` run
  `mcp+stdio://localhost/server?a`. The stand-in records `server` with `a` and refuses to start it, so no real
  process starts. They are platform-neutral: no paths, and the error text comes from the stand-in.
- Checked: `dotnet build Curl.slnx -warnaserror` is clean; 115 fast test assemblies passed and 0 failed;
  `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Conformance.Extended.UnitLibrary` reports 0 failing members.
  No package added. The CI criterion for Linux and macOS is met by keeping the change platform-neutral.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: cf4fd37d b5239a59 4b2811c8 e3597dcc.
- 2026-11-02: Doing -> Done. In-process curl and the extended harness take a stand-in extension process starter
