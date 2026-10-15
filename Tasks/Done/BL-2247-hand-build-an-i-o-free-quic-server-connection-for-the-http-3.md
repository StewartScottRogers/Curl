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
completed: 2026-10-14
---
# BL-2247 — Hand-build an I/O-free QUIC server connection for the HTTP/3 stand-in

## Goal

`Curl.Conformance.Http3Server.UnitLibrary` holds an I/O-free QUIC version 1 server connection that accepts Curl's client Initial, completes the handshake with BL-2246's TLS 1.3 server handshake, and exchanges bidirectional and unidirectional stream data with `Curl.Quic`'s `QuicClientConnectionState`, then closes.

## Context

Split out of BL-2231 (the `%HTTP3PORT` stand-in for upstream's nghttpx-h3 server). Reuse `Curl.Quic.UnitLibrary`'s codecs rather than re-writing them: `QuicPacketCodec`, `QuicFrameCodec`, `QuicInitialSecrets`, `QuicPacketKeys`, `QuicPacketProtection`, `QuicCryptoReassembler`, `QuicTransportParameters`, `QuicVariableLengthInteger` (the library may reference `Curl.Quic.UnitLibrary`; it is not a protocol library). If a type it needs is `internal`, add `InternalsVisibleTo` only if that project is in this task's `touches`; otherwise file a task to make it public.

Scope: `Receive(datagram)` returns datagrams to send. Server Initial secrets from the client's DCID; its own connection ID; CRYPTO frames at Initial and Handshake levels fed to the TLS server handshake; ACK frames per packet-number space (no loss recovery or congestion control needed in memory, but retransmit nothing and tolerate duplicates); server transport parameters (original_destination_connection_id, initial_source_connection_id, stream and data limits large enough for upstream's cases); HANDSHAKE_DONE; 1-RTT STREAM frames with offsets and FIN, MAX_DATA/MAX_STREAM_DATA/MAX_STREAMS as the client needs; CONNECTION_CLOSE in both directions; padding of the server's first Initial datagram not required but anti-amplification limit respected (3x).

## Acceptance criteria

- [x] A unit test runs `QuicClientConnectionState` against the server connection in memory: handshake completes (client reports connected with ALPN `h3`), a client bidirectional stream's bytes and FIN arrive at the server, and the server's reply bytes and FIN arrive at the client.
- [x] Tests pin: client CONNECTION_CLOSE ends the server connection; server-initiated close reaches the client; an Initial with an unknown version gets Version Negotiation; a datagram failing packet protection is dropped.
- [x] 100% line and branch coverage, complexity <= 10, CRAP <= 30 per `Measure-CodeQuality.ps1 -Library Curl.Conformance.Http3Server.UnitLibrary`; platform-neutral tests; the project's `CLAUDE.md` updated; `dotnet build -warnaserror` clean.

## Notes

- Added `Http3ServerConnection` and `Http3ServerStream` (public) and `Http3ServerPacketSpace` (internal). The library now references `Curl.Quic.UnitLibrary`. Every type it uses is public, so no `InternalsVisibleTo` was needed.
- Choice: the server's transport parameters are `DefaultTransportParameters` (2^40 data limits, 2^20 streams) plus the two connection IDs. They replace the TLS settings' own bytes, because the connection IDs are only known once the client's first Initial arrives. With limits that large the client never needs MAX_DATA, MAX_STREAM_DATA or MAX_STREAMS from the server.
- Choice: one packet per datagram, payloads of at most 1100 bytes, 4-byte packet numbers, an 8-byte server connection ID. ACKs list every received packet number with ACK Delay 0. The server never discards keys; the client discards its own.
- Choice: Version Negotiation goes out only for a datagram of 1200 bytes or more (RFC 9000 section 6.1), which also guarantees both connection IDs fit.
- Filed BL-2249 for what is left out: holding sends to the client's MAX_DATA/MAX_STREAM_DATA. Curl's 32 KiB bidi_local limit matters for replies over 32 KiB.
- Also covered the one uncovered branch in `Http3ServerTlsHandshake.ChooseSignatureScheme`: a preferred scheme the client does not offer.
- Measured with `Measure-CodeQuality.ps1 -Library Curl.Conformance.Http3Server.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. 48 tests in the project pass.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. Http3ServerConnection completes a QUIC v1 handshake with QuicClientConnectionState and carries stream data both ways in memory
