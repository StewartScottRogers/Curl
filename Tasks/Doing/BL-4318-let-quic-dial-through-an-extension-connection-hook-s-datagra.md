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
completed:
---
# BL-4318 — Let QUIC dial through an extension connection hook's datagram connector

## Goal

When an activated extension connection hook supplies a datagram connector (`IConnectionHook.DatagramConnectorOver`), HTTP/3's QUIC connections open their UDP channels through it, not through the QUIC dialer's own sockets.

## Context

Needed by BL-4144 (MASQUE: QUIC and HTTP/3 through a CONNECT-UDP or CONNECT-IP proxy). ADR-1010 lets a hook replace the datagram connector, but on the real network (`EngineDialing` null) `EngineComposition.Transports.cs`'s `DialersOf` builds a `QuicDialer` over real sockets, so the hook's datagram connector reaches only TFTP. The test path already dials QUIC over a datagram connector (`CreateDatagramConnectorQuicDialer`, `DatagramConnectorUdpChannelOpener`). `Curl.Console/CurlComposition.cs` builds the transports (line ~143) before it applies the connection hooks (`ExtensionConnectionHooksOf`), so the hook has to reach `CreateTransports` too.

## Acceptance criteria

- [ ] With a connection hook whose `DatagramConnectorOver` returns a fake datagram connector, an `--http3-only` transfer opens its QUIC channel through that connector (unit test, no network).
- [ ] Without an activated hook, or with a hook that keeps curl's datagram connector, the QUIC dialer is built exactly as before (unit test).
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3c911cfd e2979694 e35aff77 3d836d5e.
