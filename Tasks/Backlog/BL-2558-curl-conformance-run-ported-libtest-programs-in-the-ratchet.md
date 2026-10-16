---
id: BL-2558
title: Curl.Conformance: run ported libtest programs in the ratchet over a Libcurl engine on the in-memory servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2556]
touches: [Curl.Conformance.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2558 — Curl.Conformance: run ported libtest programs in the ratchet over a Libcurl engine on the in-memory servers

## Goal

`UpstreamConformanceTests` gives `UpstreamCaseRunner` an `UpstreamLibTests(LibTestPrograms.Ported, CreateEngine)` whose `CreateEngine(invocation)` is a real `Libcurl` engine (`ManagedCurlEngine` or what `CurlComposition` builds) that dials the case's in-memory servers through `InMemoryServerTcpDialer(invocation.Connector)`, `LoopbackOnlyDnsResolver`, `invocation.DatagramConnector` and the case's `<setenv>` environment, as `RunCurlAsync` composes curl, so a ported libtest program's transfers reach the sws emulation.

## Context

BL-2556 (ADR-0656) made the runner run a `<tool>` case's ported `ILibTest` in place of curl, handing it `LibTestContext.Engine = CreateEngine(invocation)`; it tested that with a stub engine, and the ratchet passes no `UpstreamLibTests` yet, so every lib case skips as "libtest program not ported". The port tasks (BL-2527 onwards) need a real engine to add cases to `PassingUpstreamCases.txt`. `ManagedCurlEngine`'s internal constructor (`Curl.Engine.UnitLibrary/ManagedCurlEngine.cs`, visible to `Curl.Conformance.UnitTests`) takes a dispatcher factory; find or add the seam that lets it take a dialer, resolver and datagram connector the way `CurlComposition.CreateRunner` does, without changing what the tool does. With `LibTestPrograms.Ported` still empty, prove it with a test-only `ILibTest` (an easy GET of `context.Url` to stdout) run through the real engine against a sws case.

## Acceptance criteria

- [ ] A test in `Curl.Conformance.UnitTests` runs a test-only `ILibTest` doing an easy GET through the ratchet's `CreateEngine`, and the case's `<verify><protocol>` and `<stdout>` match.
- [ ] `UpstreamCase_RunThroughCurl_HoldsTheRatchet` passes `UpstreamLibTests(LibTestPrograms.Ported, ...)` to the runner, and `PassingUpstreamCases.txt` is unchanged or grown with every listed case passing.
- [ ] Any production library changed: `Measure-CodeQuality.ps1 -Library` reports 0 failing members.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-16: Created.
