---
id: BL-4320
title: Open HTTP/3's QUIC channels through an extension's datagram connector
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.Transports.cs, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Console/CurlComposition.cs, Curl.Engine.UnitTests/EngineCompositionExtensionQuicTests.cs, Curl.Console.UnitTests/CurlCompositionExtensionQuicTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4320 — Open HTTP/3's QUIC channels through an extension's datagram connector

## Goal

When an extension's connection hook supplies a datagram connector (`IConnectionHook.DatagramConnectorOver`), an option group's HTTP/3 (`--http3`, `--http3-only`) QUIC connections open their UDP channels through it, as TFTP's already do.

## Context

Found by BL-4146 (`--quic-migrate`, `Curl.Extension.QuicMigration.UnitLibrary`), which wraps curl's datagram connector to move a QUIC connection to a new local address (RFC 9000 section 9.2). ADR-1010 gives the hook a datagram connector, and `Curl.Console/CurlComposition.cs` passes it to `EngineComposition.CreateProtocolHandlers` as `extensionDatagramConnector` (`ExtensionDatagramConnectorOf`). But the HTTP handler's QUIC goes through the pooling connector's `ConnectMultiplexedAsync`, whose `QuicDialer` is built in `EngineComposition.Transports.cs` (`DialersOf`): with no `EngineDialing` it is `new QuicDialer(tlsClientOptions, timeProvider)` over its own `UdpChannelOpener`, a real UDP socket, so the extension's datagram connector never sees an HTTP/3 datagram. Only the dialing harness builds `CreateDatagramConnectorQuicDialer` over `DatagramConnectorUdpChannelOpener`. The transports are built before the extension's connector is applied, so the hook (or its `Func<IDatagramConnector, IDatagramConnector>`) has to reach `CreateTransports` too.

## Acceptance criteria

- [ ] With a connection hook whose `DatagramConnectorOver` returns its own connector, an `https://` transfer under `--http3-only` opens its QUIC channel through that connector's `OpenAsync` (host and port of the URL); unit test with a recording connector, no network.
- [ ] With no connection hook active, or a hook that keeps curl's datagram connector, the QUIC dialer is built exactly as before (its own `UdpChannelOpener`); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records how the hook reaches the transports.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
