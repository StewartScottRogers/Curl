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
completed:
---
# BL-278 — Pass the composition root's TimeProvider to SslStreamTlsProvider in CurlComposition

## Goal

`CurlComposition` constructs `SslStreamTlsProvider` with the same `TimeProvider` it gives `TcpConnector`, so `ConnectTimings.TlsHandshakeCompleted` is on the connector's clock.

## Context

- ADR-0030 (from BL-211): `SslStreamTlsProvider` reports its handshake timestamps on the `TimeProvider` it is constructed with, `TimeProvider.System` by default, and `TcpConnector` keeps its `TlsHandshakeCompleted`. The two are only comparable when both share one provider.
- `Curl.Console/CurlComposition.cs` line 67 constructs `new SslStreamTlsProvider(tlsClientOptions)`; use the `SslStreamTlsProvider(TlsClientOptions, TimeProvider)` overload with the `timeProvider` already passed to `TcpConnector`.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests` (beside `CurlCompositionTests`) shows the `TimeProvider` captured by the composed `SslStreamTlsProvider` is the one given to `TcpConnector`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no new failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
