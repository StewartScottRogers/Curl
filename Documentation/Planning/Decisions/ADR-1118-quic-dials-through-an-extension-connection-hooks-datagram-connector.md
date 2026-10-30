# ADR-1118 — QUIC dials through an extension connection hook's datagram connector

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4318.

## Context

ADR-1010 lets an activated extension connection hook replace curl's datagram connector
(`IConnectionHook.DatagramConnectorOver`), but the executable's transports built HTTP/3's `QuicDialer` over its own
UDP sockets, so the hook's connector reached only the datagram protocols (TFTP). MASQUE (BL-4144) needs QUIC to open
its channels through the hook. Upstream curl has no extension hooks, so there is no curl behaviour to match; a run
with no hook must stay exactly as it was.

## Decision

1. `EngineComposition.CreateTransports` takes an optional `extensionDatagramConnector`, the hook's
   `DatagramConnectorOver`. On the real network (no `EngineDialing`) it hands the hook curl's UDP datagram connector;
   when the hook gives back another connector, the QUIC dialer opens its channels through it
   (`DatagramConnectorUdpChannelOpener`, as the dialing runner's does), on the transports' clock.
2. With no hook, or a hook that gives back curl's own connector (the interface's default), the socket `QuicDialer`
   is built exactly as before (`QuicDatagramConnectorOf` answers `null`).
3. `CurlTransports.UdpDatagramConnector` stays curl's, so the dispatch hands it to the hook again for the datagram
   protocols as ADR-1010 says. The hook's `DatagramConnectorOver` is therefore called twice per option group, once
   for QUIC and once for the datagram protocols; a hook's connector must not assume it is made once.
4. Under `EngineDialing` (the conformance harness) the hook is not consulted for QUIC: the harness's datagram
   connector stays the one QUIC dials through.
5. `Curl.Console`'s `CurlComposition.CreateNetworkTransports` activates the group's connection hook once and passes it
   both to the transports and to the dispatch.

## Consequences

An extension that tunnels datagrams (CONNECT-UDP, CONNECT-IP) carries `--http3` and `--http3-only` transfers with
no change to the QUIC stack. Tests: `EngineCompositionTests.QuicThroughExtension.cs` and
`CurlCompositionQuicThroughExtensionTests`.
