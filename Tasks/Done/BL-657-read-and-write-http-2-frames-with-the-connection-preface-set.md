---
id: BL-657
title: Read and write HTTP/2 frames with the connection preface, SETTINGS and flow control
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-655, BL-715]
touches: [Curl.Http2.UnitLibrary, Curl.Http2.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-657 — Read and write HTTP/2 frames with the connection preface, SETTINGS and flow control

## Goal

An HTTP/2 connection layer in `Curl.Http2.UnitLibrary`, over a byte stream the caller hands in (in production the `IConnection`'s stream, passed by the HTTP handler), sends the client preface and the SETTINGS (and initial WINDOW_UPDATE) curl 8.21.0's library sends, reads and writes every frame type of RFC 9113, acknowledges SETTINGS and PING, tracks connection and stream flow-control windows, and turns GOAWAY, RST_STREAM and protocol errors into typed failures.

## Context

- Conformance audit 2026-09-28, row 32. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): HTTP/2 is offered on every platform; the hand-built frame layer lives in `Curl.Http2.UnitLibrary` (BL-715) with HPACK (BL-656), and allocates stream IDs so several streams can share the connection (BL-717 multiplexes on it).
- The preface and SETTINGS values curl sends are measured in BL-655 (record them there if missing: `Record-CurlExchange.ps1` with `--http2-prior-knowledge` on an OpenSSL build captures the preface bytes in `request.bin`).

## Acceptance criteria

- [x] `Curl.Http2.UnitTests` pin the preface and SETTINGS bytes as measured, and frame round trips and error handling for each frame type through an in-memory stream, including frames split across reads and flow-control exhaustion.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Http2.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-09-28 with curl.se's Windows build (curl 8.18.0, LibreSSL, nghttp2 1.68.0; the only local nghttp2 build) via `Record-CurlExchange.ps1 -Curl <WinGet curl> --http2-prior-knowledge http://127.0.0.1:18657/ -HoldOpenMilliseconds 1500`: the preface, then SETTINGS `MAX_CONCURRENT_STREAMS 100, INITIAL_WINDOW_SIZE 65536, ENABLE_PUSH 0` (the same values as the `HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA` ADR-0141 recorded from Linux curl), then a connection WINDOW_UPDATE of 1048510465 (window 1000 MiB), all in one write. Pinned byte for byte in `Http2ConnectionTests.SendPrefaceAsync_WritesThePrefaceSettingsAndWindowUpdateCurlSends`.
- The same capture shows curl then sends HEADERS on stream 1 and two stream WINDOW_UPDATEs of 10420225 (raising the stream window to about 10 MiB). That is per-request behaviour for BL-658; `Http2Connection.IncreaseStreamReceiveWindowAsync` is there for it.
- Design (default taken, per the task's scope): `Http2FrameCodec` (wire), `Http2FrameFactory` (create each type), `Http2FramePayloadParser` (read and validate each type), `Http2Settings`, `Http2FlowControlWindow`, and `Http2Connection` over a caller-owned `Stream`. Every violation detected locally is a connection error: GOAWAY is sent and `Http2ProtocolException` thrown (RFC 9113 section 5.4.1 allows treating a stream error as a connection error). The peer's RST_STREAM becomes `Http2StreamResetException` (connection stays usable) and its GOAWAY `Http2GoAwayException` when it carries an error or leaves an open stream unprocessed; a graceful GOAWAY covering every open stream lets them finish.
- `WriteDataAsync` never waits for window: it sends what the windows allow and returns the count; the caller reads frames (applying WINDOW_UPDATE) and sends the rest. Receive windows are topped back up once half is used, as nghttp2 does, so a well-behaved peer never stalls.
- A header block may take at most 8 CONTINUATION frames, nghttp2's default (`NGHTTP2_DEFAULT_MAX_CONTINUATIONS`), so empty-CONTINUATION floods end in ENHANCE_YOUR_CALM. PUSH_PROMISE, and a server's SETTINGS_ENABLE_PUSH of 1, are PROTOCOL_ERROR since the client sends ENABLE_PUSH 0. No timers here, so no `TimeProvider` yet: SETTINGS-ack and PING timeouts belong to whoever needs them.
- Code review (code-reviewer agent) fixes, second commit: RST_STREAM(NO_ERROR) after the response ended keeps the response (RFC 9113 section 8.1); closed streams are forgotten; DATA before HEADERS and out-of-order stream starts are refused; state changes only after a write succeeds; a failed connection refuses further use and a failed GOAWAY write never hides the protocol error; RST_STREAM on an idle stream is refused.
- Measure-CodeQuality: 100% line, 100% branch, 144 members, worst CRAP 10. `Curl.Http2.UnitTests`: 216 tests.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Http2 frame layer: curl's measured preface, SETTINGS and WINDOW_UPDATE, every RFC 9113 frame type, flow control and typed GOAWAY/RST_STREAM/protocol errors, 100% covered
