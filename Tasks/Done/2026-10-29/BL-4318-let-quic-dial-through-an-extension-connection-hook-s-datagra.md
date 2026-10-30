---
id: BL-4318
title: Let QUIC dial through an extension connection hook's datagram connector
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.Transports.cs, Curl.Console/CurlComposition.cs, Curl.Engine.UnitTests/EngineCompositionTests.QuicThroughExtension.cs, Curl.Console.UnitTests/CurlCompositionQuicThroughExtensionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4318 — Let QUIC dial through an extension connection hook's datagram connector

## Goal

When an activated extension connection hook supplies a datagram connector (`IConnectionHook.DatagramConnectorOver`), HTTP/3's QUIC connections open their UDP channels through it, not through the QUIC dialer's own sockets.

## Context

Needed by BL-4144 (MASQUE: QUIC and HTTP/3 through a CONNECT-UDP or CONNECT-IP proxy). ADR-1010 lets a hook replace the datagram connector, but on the real network (`EngineDialing` null) `EngineComposition.Transports.cs`'s `DialersOf` builds a `QuicDialer` over real sockets, so the hook's datagram connector reaches only TFTP. The test path already dials QUIC over a datagram connector (`CreateDatagramConnectorQuicDialer`, `DatagramConnectorUdpChannelOpener`). `Curl.Console/CurlComposition.cs` builds the transports (line ~143) before it applies the connection hooks (`ExtensionConnectionHooksOf`), so the hook has to reach `CreateTransports` too.

## Acceptance criteria

- [x] With a connection hook whose `DatagramConnectorOver` returns a fake datagram connector, an `--http3-only` transfer opens its QUIC channel through that connector (unit test, no network).
- [x] Without an activated hook, or with a hook that keeps curl's datagram connector, the QUIC dialer is built exactly as before (unit test).
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan (ADR-1118): `CreateTransports` takes an optional `extensionDatagramConnector`; on the real network
  `NetworkDialersOf` builds the QUIC dialer over `DatagramConnectorUdpChannelOpener` when the hook's connector is not
  curl's own (`QuicDatagramConnectorOf`), else the socket `QuicDialer` as before. `CurlTransports.UdpDatagramConnector`
  stays curl's so the dispatch still hands it to the hook for TFTP; the hook's `DatagramConnectorOver` is so called
  twice per option group (default taken: simplest change, recorded in the ADR). `Curl.Console`'s new internal
  `CreateNetworkTransports` activates the hook once and passes it to both.
- Tests: `EngineCompositionTests.QuicThroughExtension.cs` (5) and `CurlCompositionQuicThroughExtensionTests` (2) drive
  `TcpConnector.ConnectMultiplexedAsync` for a QUIC-build `--http3-only` group with a refusing fake connector.
- `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console`: 0 failing members.
- CI on Linux and macOS: the tests use no path, OS error text or certificate, and the fake connector refuses at once,
  so they are platform-neutral; fast tests green here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3c911cfd e2979694 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. QUIC opens its channels through an activated extension connection hook's datagram connector on the real network
