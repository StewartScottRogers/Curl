---
id: BL-2247
title: Hand-build an I/O-free QUIC server connection for the HTTP/3 stand-in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2246]
touches: [Curl.Conformance.Http3Server.UnitLibrary, Curl.Conformance.Http3Server.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2247 — Hand-build an I/O-free QUIC server connection for the HTTP/3 stand-in

## Goal

`Curl.Conformance.Http3Server.UnitLibrary` holds an I/O-free QUIC version 1 server connection that accepts Curl's client Initial, completes the handshake with BL-2246's TLS 1.3 server handshake, and exchanges bidirectional and unidirectional stream data with `Curl.Quic`'s `QuicClientConnectionState`, then closes.

## Context

Split out of BL-2231 (the `%HTTP3PORT` stand-in for upstream's nghttpx-h3 server). Reuse `Curl.Quic.UnitLibrary`'s codecs rather than re-writing them: `QuicPacketCodec`, `QuicFrameCodec`, `QuicInitialSecrets`, `QuicPacketKeys`, `QuicPacketProtection`, `QuicCryptoReassembler`, `QuicTransportParameters`, `QuicVariableLengthInteger` (the library may reference `Curl.Quic.UnitLibrary`; it is not a protocol library). If a type it needs is `internal`, add `InternalsVisibleTo` only if that project is in this task's `touches`; otherwise file a task to make it public.

Scope: `Receive(datagram)` returns datagrams to send. Server Initial secrets from the client's DCID; its own connection ID; CRYPTO frames at Initial and Handshake levels fed to the TLS server handshake; ACK frames per packet-number space (no loss recovery or congestion control needed in memory, but retransmit nothing and tolerate duplicates); server transport parameters (original_destination_connection_id, initial_source_connection_id, stream and data limits large enough for upstream's cases); HANDSHAKE_DONE; 1-RTT STREAM frames with offsets and FIN, MAX_DATA/MAX_STREAM_DATA/MAX_STREAMS as the client needs; CONNECTION_CLOSE in both directions; padding of the server's first Initial datagram not required but anti-amplification limit respected (3x).

## Acceptance criteria

- [ ] A unit test runs `QuicClientConnectionState` against the server connection in memory: handshake completes (client reports connected with ALPN `h3`), a client bidirectional stream's bytes and FIN arrive at the server, and the server's reply bytes and FIN arrive at the client.
- [ ] Tests pin: client CONNECTION_CLOSE ends the server connection; server-initiated close reaches the client; an Initial with an unknown version gets Version Negotiation; a datagram failing packet protection is dropped.
- [ ] 100% line and branch coverage, complexity <= 10, CRAP <= 30 per `Measure-CodeQuality.ps1 -Library Curl.Conformance.Http3Server.UnitLibrary`; platform-neutral tests; the project's `CLAUDE.md` updated; `dotnet build -warnaserror` clean.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
