---
id: BL-278
title: Pass the composition root's TimeProvider to SslStreamTlsProvider in CurlComposition
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-211]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-278 — Pass the composition root's TimeProvider to SslStreamTlsProvider in CurlComposition

## Goal

`CurlComposition` constructs `SslStreamTlsProvider` with the same `TimeProvider` it gives `TcpConnector`, so `ConnectTimings.TlsHandshakeCompleted` is on the connector's clock.

## Context

- ADR-0030 (from BL-211): `SslStreamTlsProvider` reports its handshake timestamps on the `TimeProvider` it is constructed with, `TimeProvider.System` by default, and `TcpConnector` keeps its `TlsHandshakeCompleted`. The two are only comparable when both share one provider.
- `Curl.Console/CurlComposition.cs` line 67 constructs `new SslStreamTlsProvider(tlsClientOptions)`; use the `SslStreamTlsProvider(TlsClientOptions, TimeProvider)` overload with the `timeProvider` already passed to `TcpConnector`.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests` (beside `CurlCompositionTests`) shows the `TimeProvider` captured by the composed `SslStreamTlsProvider` is the one given to `TcpConnector`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no new failing member for `Curl.Console`.

## Notes

- Choice (sensible default): `CreateTransports(CommandLineOptions)` now delegates to a new internal overload `CreateTransports(CommandLineOptions, TimeProvider)`. Both the default and the fake would be `TimeProvider.System` otherwise, so a test could not tell a shared clock from two separate defaults; the overload lets `CreateTransports_GivenTimeProvider_SslStreamTlsProviderSharesTheTcpConnectorsTimeProvider` pass a distinct provider and see it in `TcpConnector`, `SslStreamTlsProvider` and `UdpDatagramConnector`. Production still uses `TimeProvider.System`.
- `Measure-CodeQuality.ps1`: Curl.Console 100% line, 100% branch, 0 failing members, worst CRAP 10. The 4 failing members it reports are in Curl.Networking.UnitLibrary, which this task does not touch.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. CurlComposition gives SslStreamTlsProvider the same TimeProvider as TcpConnector, so TLS handshake timestamps are on the connector's clock
