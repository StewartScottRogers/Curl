---
id: BL-2249
title: Hold the HTTP/3 stand-in's sending to the client's QUIC flow control limits
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2247]
touches: [Curl.Conformance.Http3Server.UnitLibrary, Curl.Conformance.Http3Server.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2249 — Hold the HTTP/3 stand-in's sending to the client's QUIC flow control limits

## Goal

Http3ServerConnection never sends stream data past the client's MAX_DATA or MAX_STREAM_DATA limits; it holds the rest until the client raises them.

## Context

BL-2247 left the server sending without regard to the client's flow control. Curl's client declares initial_max_stream_data_bidi_local 32768 (QuicTransportParameters.CurlClientDefaults), so a reply body over 32 KiB makes Curl's QuicStreamSet close with FLOW_CONTROL_ERROR. Read the client's limits from Http3ServerTlsHandshake.ClientQuicTransportParameters (QuicTransportParameters.Decode), track per-stream and connection send credit, raise it on MAX_DATA and MAX_STREAM_DATA, and queue unsent STREAM data per stream in Http3ServerConnection.Write.

## Acceptance criteria

- [x] A unit test writes 100000 bytes with FIN on a client bidirectional stream against QuicClientConnectionState and every byte and the FIN arrive with no client failure.
- [x] 100% line and branch coverage, complexity <= 10 per Measure-CodeQuality.ps1 -Library Curl.Conformance.Http3Server.UnitLibrary; the project's CLAUDE.md updated.

## Notes

- Http3ServerSendStream (internal) holds each stream's unsent bytes, pending FIN and MAX_STREAM_DATA limit; Http3ServerConnection reads the client's limits from ClientQuicTransportParameters when the handshake completes and queues STREAM frames only within MAX_DATA and the stream limit, again on each MAX_DATA or MAX_STREAM_DATA. A client bidirectional stream takes initial_max_stream_data_bidi_local, a server unidirectional one initial_max_stream_data_uni (RFC 9000 18.2). Write of no bytes without FIN now sends nothing (before, an empty STREAM frame).
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Http3Server.UnitLibrary: 0 failing members.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. The HTTP/3 stand-in holds STREAM data to the client's MAX_DATA and MAX_STREAM_DATA limits; a 100000-byte reply reaches Curl's QUIC client whole
