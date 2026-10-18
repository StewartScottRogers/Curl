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
completed: 2026-10-16
---
# BL-2558 — Curl.Conformance: run ported libtest programs in the ratchet over a Libcurl engine on the in-memory servers

## Goal

`UpstreamConformanceTests` gives `UpstreamCaseRunner` an `UpstreamLibTests(LibTestPrograms.Ported, CreateEngine)` whose `CreateEngine(invocation)` is a real `Libcurl` engine (`ManagedCurlEngine` or what `CurlComposition` builds) that dials the case's in-memory servers through `InMemoryServerTcpDialer(invocation.Connector)`, `LoopbackOnlyDnsResolver`, `invocation.DatagramConnector` and the case's `<setenv>` environment, as `RunCurlAsync` composes curl, so a ported libtest program's transfers reach the sws emulation.

## Context

BL-2556 (ADR-0656) made the runner run a `<tool>` case's ported `ILibTest` in place of curl, handing it `LibTestContext.Engine = CreateEngine(invocation)`; it tested that with a stub engine, and the ratchet passes no `UpstreamLibTests` yet, so every lib case skips as "libtest program not ported". The port tasks (BL-2527 onwards) need a real engine to add cases to `PassingUpstreamCases.txt`. `ManagedCurlEngine`'s internal constructor (`Curl.Engine.UnitLibrary/ManagedCurlEngine.cs`, visible to `Curl.Conformance.UnitTests`) takes a dispatcher factory; find or add the seam that lets it take a dialer, resolver and datagram connector the way `CurlComposition.CreateRunner` does, without changing what the tool does. With `LibTestPrograms.Ported` still empty, prove it with a test-only `ILibTest` (an easy GET of `context.Url` to stdout) run through the real engine against a sws case.

## Acceptance criteria

- [x] A test in `Curl.Conformance.UnitTests` runs a test-only `ILibTest` doing an easy GET through the ratchet's `CreateEngine`, and the case's `<verify><protocol>` and `<stdout>` match.
- [x] `UpstreamCase_RunThroughCurl_HoldsTheRatchet` passes `UpstreamLibTests(LibTestPrograms.Ported, ...)` to the runner, and `PassingUpstreamCases.txt` is unchanged or grown with every listed case passing.
- [x] Any production library changed: `Measure-CodeQuality.ps1 -Library` reports 0 failing members.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Seam (ADR-0658): new internal `EngineDialing(ITcpDialer, IDnsResolver, IDatagramConnector)` in `Curl.Engine`, taken by `ManagedCurlEngine`'s internal constructor and kept on `ManagedEngineSharedState.Dialing`; `EngineComposition.CreateTransports` (the `wrapLookUps` overload) builds the `TcpConnector` over its dialer, uses its resolver and datagram connector, and a datagram-connector QUIC dialer, as `CurlComposition`'s dialing runner does. `CurlTransports.UdpDatagramConnector` is now an `IDatagramConnector`. Without a dialing nothing changes. Chosen over a test-side dispatcher copy so the handle's callbacks, connect-only connector and WebSocket authenticator all dial the in-memory servers too.
- `UpstreamConformanceTests.CreateEngine` builds the engine (system clock, `Libcurl`'s `CurlEasyErrorText.Of`, the case's `<setenv>` as the proxy environment); the ratchet's `RunCaseOnceAsync` passes `UpstreamLibTests(LibTestPrograms.Ported, CreateEngine)`. `PassingUpstreamCases.txt` unchanged (no program is ported yet).
- Test: `CreateEngine_EasyGetLibTestOnAnSwsCase_MatchesTheCaseProtocolAndStdout`, a test-only `lib9558` doing an easy GET to stdout against the sws emulation. Two gaps found and filed rather than fixed (outside touches): the engine sends `User-Agent: curl/8.22.0` without `CURLOPT_USERAGENT` where libcurl sends none (BL-2559; the test program sets its own UA meanwhile), and the runner judges a lib case's `<reply><data>` against the `--output` file a program never writes (BL-2560; the test case's data is `nocheck` meanwhile). Port tasks such as lib500's will need both.
- Quality: first `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` run failed `CreateTransports` on complexity; moved the dialing choices into `ResolverOf` and `DialersOf`; the second run reports 0 failing members, 100% line and branch.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 4e19d2f4 9b707145 3160c91e 4272a6b9.
- 2026-10-16: Doing -> Done. The ratchet runs ported libtest programs over a ManagedCurlEngine that dials the case's in-memory servers (EngineDialing, ADR-0658); a test-only easy GET matches its sws case
