---
id: BL-838
title: Stream large HTTP/3 DATA frames instead of reading each whole
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-731]
touches: [Curl.Http3.UnitLibrary, Curl.Http3.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-838 — Stream large HTTP/3 DATA frames instead of reading each whole

## Goal

An HTTP/3 response whose `DATA` frame is longer than 16 MiB downloads in full, its payload handed to the transfer in bounded chunks as it arrives, as nghttp3 streams `DATA` of any length; only a non-`DATA` frame over the limit still fails with exit 56 and `ERR_H3_EXCESSIVE_LOAD`.

## Context

- `Curl.Http3.UnitLibrary/Http3FrameReader.cs` reads a frame's whole payload into memory (`ReadFrameAsync` → `ReadPayloadAsync`) and throws `Http3Exception(Http3ErrorCode.ExcessiveLoad)` when a payload is longer than the `maximumPayloadLength` it was built with; `Http3DataFrame` holds its whole `Payload`. So `Curl.Protocol.Http.UnitLibrary/Http3StreamConnection.cs` builds its reader with `MaximumFramePayloadLength = 16 * 1024 * 1024` and a server sending one 17 MiB `DATA` frame fails the transfer with exit 56 `nghttp3_conn_read_stream returned error: ERR_H3_EXCESSIVE_LOAD` (BL-731, ADR-0172, `Documentation/Planning/Decisions/ADR-0172-http-3-requests-run-on-an-http3session-and-fall-back-to-tcp-only-when-quic-fails.md`). nghttp3 (1.15, the version in curl.se's 8.18.0 build, ADR-0144) never buffers a `DATA` payload; it passes each received piece to curl's `cb_h3_recv_data` in `lib/vquic/curl_ngtcp2.c` at tag `curl-8_18_0`.
- Design to implement: in `Curl.Http3.UnitLibrary`, add a way to read a request stream where `DATA` payload bytes are copied into a caller's buffer as they arrive (a `DATA` frame spanning several calls), while every other frame type is still returned whole and limited by `maximumPayloadLength`. Name it for what it does, e.g. `Http3FrameReader.ReadFrameOrDataAsync(Memory<byte> buffer, CancellationToken)` returning a small result type that is either a whole non-`DATA` frame, a count of `DATA` payload bytes written into `buffer`, or end of stream. Keep `ReadFrameAsync` and its behaviour for the control stream (`Http3ControlStreamReader`) and existing callers. A stream ending inside a `DATA` payload is still `Http3ErrorCode.FrameError`; a zero-length `DATA` frame is legal and yields nothing.
- Then `Http3StreamConnection` reads response content through the new call, so the memory it holds per stream is bounded by its read buffer, not by a frame's length; `MaximumFramePayloadLength` then applies only to `HEADERS` (and any other non-`DATA` frame). Its existing checks stay: `DATA` before `HEADERS` is exit 56 `ERR_H3_FRAME_UNEXPECTED`, a reset is exit 95/18, trailers after `DATA` still work.
- The Curl.Http3 project's own `CLAUDE.md` applies to its part.

## Acceptance criteria

- [ ] `Curl.Http3.UnitTests/Http3FrameReaderTests.cs` has tests that pin: a `DATA` frame whose payload is longer than `maximumPayloadLength` is read in full across several calls with a small buffer and no exception; the byte counts sum to the frame length and the bytes are the payload in order; a `HEADERS` frame over the limit still throws `Http3Exception` with `Http3ErrorCode.ExcessiveLoad`; the stream ending mid-`DATA` throws with `Http3ErrorCode.FrameError`; a zero-length `DATA` frame followed by `HEADERS` returns the `HEADERS` frame; unknown and reserved frame types interleaved between `DATA` frames are skipped or refused exactly as `ReadFrameAsync` does.
- [ ] `Curl.Protocol.Http.UnitTests/Http3StreamConnectionTests.cs` has a test in which the fake server sends one `DATA` frame of `Http3StreamConnection.MaximumFramePayloadLength + 1` bytes and the response body delivered to the transfer is exactly those bytes with exit 0, and a test that a `HEADERS` frame over the limit still fails with exit 56 and `nghttp3_conn_read_stream returned error: ERR_H3_EXCESSIVE_LOAD`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary` and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` each report 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
