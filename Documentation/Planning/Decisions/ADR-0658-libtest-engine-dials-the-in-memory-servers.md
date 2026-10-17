# ADR-0658 — The ratchet's libtest engine dials the in-memory servers through `EngineDialing`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2558.

## Context

BL-2556 (ADR-0656) made `UpstreamCaseRunner` run a `<tool>` case's ported `ILibTest` in place of curl, handing it
`LibTestContext.Engine = UpstreamLibTests.CreateEngine(invocation)`, but the ratchet passed no `UpstreamLibTests`, so
every lib case skipped. A ported program needs a real `Libcurl` engine whose transfers reach the case's in-memory
servers (the sws emulation and the rest) the way the ratchet's curl does: `UpstreamConformanceTests.RunCurlAsync`
gives `CurlComposition.CreateRunner` an `InMemoryServerTcpDialer`, a `LoopbackOnlyDnsResolver`, the invocation's
datagram connector and the case's `<setenv>` environment, so `TcpConnector`'s proxy, TLS and connect code stays in
the path (ADR-0460).

`ManagedCurlEngine`'s internal constructor took only a dispatcher factory. Its production `CreateDispatcher` builds
each transfer's transports with `EngineComposition.CreateTransports`, which made its own `TcpDialer`, resolver and
`UdpDatagramConnector`. A test-side copy of the dispatcher would miss the handle's callbacks (prerequest, socket,
resolver-start, wildcard, interleave), the connect-only connector and the WebSocket authenticator, which all build on
the same transports.

## Decision

1. A new internal record `EngineDialing(ITcpDialer TcpDialer, IDnsResolver DnsResolver, IDatagramConnector
   DatagramConnector)` is what an engine dials, resolves and opens datagram channels through in place of sockets.
   `ManagedCurlEngine`'s internal constructor takes one (`dialing`, `null` by default) and keeps it on
   `ManagedEngineSharedState.Dialing`, which a share handle's state copies from the engine's.
2. `EngineComposition.CreateTransports` (the `wrapLookUps` overload, which the engine calls) takes it as a last
   optional parameter. With one, as `CurlComposition`'s dialing runner composes: the `TcpConnector` dials through its
   dialer, its resolver is the transports' `DnsResolver` (so `--doh-url` and the c-ares options pick nothing), the
   datagram protocols open through its connector, and QUIC through `CreateDatagramConnectorQuicDialer` over the same
   connector. `CurlTransports.UdpDatagramConnector` becomes an `IDatagramConnector` to hold it. Without one nothing
   changes, so neither the curl tool nor `Libcurl` and `Curl.Api` behave differently.
3. `UpstreamConformanceTests.CreateEngine` builds the ratchet's libtest engine: `ManagedCurlEngine` on the system
   clock, curl's easy error text (`Libcurl`'s `CurlEasyErrorText.Of`), the case's `<setenv>` variables as the proxy
   environment, and an `EngineDialing` over `InMemoryServerTcpDialer(invocation.Connector)`,
   `LoopbackOnlyDnsResolver` and `invocation.DatagramConnector`. The ratchet passes
   `UpstreamLibTests(LibTestPrograms.Ported, CreateEngine)` to its runner.

## Consequences

- A ported program's transfers reach the case's servers through the engine's own production transports, so the
  port tasks (BL-2527 onwards) can list lib cases on `PassingUpstreamCases.txt`.
- Proving it with a test-only easy GET found two gaps, filed rather than fixed here: the engine sends the tool's
  `User-Agent: curl/8.22.0` without `CURLOPT_USERAGENT`, where libcurl sends none (BL-2559), and the runner compares
  a lib case's `<reply><data>` with the `--output` file a program never writes (BL-2560).
