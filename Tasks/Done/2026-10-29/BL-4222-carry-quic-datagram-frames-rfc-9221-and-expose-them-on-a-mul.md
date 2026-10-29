---
id: BL-4222
title: Carry QUIC DATAGRAM frames (RFC 9221) and expose them on a multiplexed connection
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Quic.UnitLibrary/QuicFrameType.cs, Curl.Quic.UnitLibrary/QuicFrameCodec.cs, Curl.Quic.UnitLibrary/QuicDatagramFrame.cs, Curl.Quic.UnitLibrary/QuicTransportParameters.cs, Curl.Quic.UnitLibrary/QuicClientSettings.cs, Curl.Quic.UnitLibrary/QuicClientConnectionState.cs, Curl.Quic.UnitLibrary/QuicConnection.cs, Curl.Quic.UnitTests, Curl.Protocol.Abstractions.UnitLibrary/IDatagramCarryingConnection.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4222 — Carry QUIC DATAGRAM frames (RFC 9221) and expose them on a multiplexed connection

## Goal

Curl's QUIC client negotiates `max_datagram_frame_size` and sends and receives DATAGRAM frames (RFC 9221), and a caller holding the `IMultiplexedConnection` can send and receive datagrams through a new optional interface in `Curl.Protocol.Abstractions.UnitLibrary`.

## Context

Found by BL-4113 (WebTransport extension), which needs HTTP/3 datagrams (RFC 9297 section 2.1) and so QUIC DATAGRAM frames; BL-4144 (MASQUE) needs them too. Today `Curl.Quic.UnitLibrary` has no frame type 0x30/0x31 and no transport parameter 0x20. Add the interface as a new file (an optional interface QuicConnection implements, say `IDatagramCarryingConnection`), not a member on `IMultiplexedConnection`, so no other implementation of that interface (Engine's socket-callback wrapper, test fakes) changes. A caller opts in through `QuicClientSettings` (off by default, so curl's own HTTP/3 never advertises it). Check whether a connector wrapper (PoolingConnector, CertificateCheckingConnector) hides the interface from an extension dialling through `IConnector` as `DnsOverQuicResolver` does; if one does, file that as a task.

## Acceptance criteria

- [x] DATAGRAM frames (types 0x30 and 0x31) encode and decode as RFC 9221 section 4 gives, and `max_datagram_frame_size` (0x20) as section 3 gives (unit tests on bytes).
- [x] A connection whose peer did not advertise `max_datagram_frame_size` refuses to send a datagram, and a DATAGRAM frame received when the client did not advertise it closes the connection with PROTOCOL_VIOLATION (RFC 9221 section 3; unit tests).
- [x] DATAGRAM frames are ack-eliciting and never retransmitted (section 5; unit test).
- [x] With the setting off, the client's transport parameters are byte-for-byte what they are today (unit test).
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decisions in ADR-1043: opt-in is `QuicClientSettings.TransportParameters` with a non-zero `MaxDatagramFrameSize` (default 0, never written), not a second setting that would duplicate it; one datagram per packet, held by the congestion window; at most 1100 payload bytes per datagram; receive and send checks as RFC 9221 section 3 gives.
- `QuicConnection` implements the new `IDatagramCarryingConnection` (Curl.Protocol.Abstractions.UnitLibrary/IDatagramCarryingConnection.cs).
- Connector wrappers: `PoolingConnector.ConnectMultiplexedAsync` passes the inner connection through and `CertificateCheckingConnector` does not wrap it; Engine's `SocketCallbackMultiplexedConnection` hides the interface, filed as BL-4254.
- `QuicAdversarialTests.Decode_UnknownFrameType_IsAFrameEncodingError` had a row for 0x30 as unknown; 0x30 is now a known frame (refused by the connection when not negotiated, covered by `QuicClientDatagramTests`), so that row is now 0x32.
- Setting off: `Encode_CurlClientDefaults_MatchesCurlsBuild` still pins the unchanged bytes, and `Decode_NoMaxDatagramFrameSize_IsZeroAndCurlsDefaultsDoNotSendIt` checks the default.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary`: 100% line, 100% branch, 581 members, 0 failing, worst CRAP 10. Curl.Protocol.Abstractions.UnitLibrary gained only an interface with no executable code, so it was not measured separately (it reaches nearly every test project); complexity is enforced by the clean build.
- Full `dotnet build` clean; fast tests all green (Curl.Quic.UnitTests 505). Tests are platform-neutral (in-memory server, no paths, OS errors or certificates).

## Log

- 2026-10-29: Created by BL-4113's lane 7 run as its prerequisite.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 7c3a7b75 b0eb394b ffb316e0 7751c039 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. QUIC carries RFC 9221 DATAGRAM frames, negotiates max_datagram_frame_size opt-in, and QuicConnection exposes them through IDatagramCarryingConnection
