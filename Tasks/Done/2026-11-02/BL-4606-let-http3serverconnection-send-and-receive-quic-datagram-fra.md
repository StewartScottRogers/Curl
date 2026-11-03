---
id: BL-4606
title: Let Http3ServerConnection send and receive QUIC DATAGRAM frames (RFC 9221) for the WebTransport reference server
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Http3Server.UnitLibrary/Http3ServerConnection.cs, Curl.Conformance.Http3Server.UnitTests/Http3ServerConnectionDatagramTests.cs, Curl.Conformance.Http3Server.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4606 — Let Http3ServerConnection send and receive QUIC DATAGRAM frames (RFC 9221) for the WebTransport reference server

## Goal

`Http3ServerConnection` receives the QUIC DATAGRAM frames a client sends and sends its own, so a reference server on it can carry HTTP datagrams (RFC 9297) for WebTransport.

## Context

Split from BL-4494 (WebTransport conformance). The frame switch in `Curl.Conformance.Http3Server.UnitLibrary/Http3ServerConnection.cs` (near line 336) handles CRYPTO, STREAM, MAX_DATA, MAX_STREAM_DATA and CONNECTION_CLOSE but not DATAGRAM (types 0x30 and 0x31), so a client's datagram is lost and the server cannot send one. The codec already exists: `Curl.Quic.UnitLibrary/QuicDatagramFrame.cs` and `QuicFrameCodec`. The server advertises `max_datagram_frame_size` through the transport parameters in `Http3ServerTlsSettings`. RFC 9221 section 3 says a peer MUST NOT send DATAGRAM frames unless the other side advertised that parameter, so the server sends none when the client did not advertise it.

## Acceptance criteria

- [x] `Http3ServerConnection` queues every DATAGRAM frame the client sends, in arrival order, readable through a public method (for example `AcceptDatagram()`, returning the payload or null).
- [x] `Http3ServerConnection` has a public method that sends one DATAGRAM frame in the next 1-RTT packet, and throws `InvalidOperationException` when the client did not advertise `max_datagram_frame_size` or the payload exceeds it.
- [x] Unit tests in `Curl.Conformance.Http3Server.UnitTests/Http3ServerConnectionDatagramTests.cs` pin both directions against the Curl.Quic client; the library stays at 100% line and branch coverage and complexity at most 10 (`Measure-CodeQuality.ps1 -Library Curl.Conformance.Http3Server.UnitLibrary`).
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; the library's `CLAUDE.md` names the datagram support.

## Notes

- Server now advertises max_datagram_frame_size 1200 in DefaultTransportParameters (needed so a client may send datagrams). API: AcceptDatagram(), SendDatagram(ReadOnlySpan<byte>). ReceiveFrame split to stay under complexity 10. Quality run: 100%/100%, 0 failing. Fast tests green (68).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 7cbc8046 103b7afc 286a1d56 481bd9fe.
- 2026-11-02: Doing -> Done. Datagram send and receive built, tested, 100% covered
