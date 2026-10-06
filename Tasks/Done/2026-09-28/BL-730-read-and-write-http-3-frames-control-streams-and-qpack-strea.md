---
id: BL-730
title: Read and write HTTP/3 frames, control streams and QPACK streams
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-729]
touches: [Curl.Http3.UnitLibrary, Curl.Http3.UnitTests, Documentation/Planning/Decisions/ADR-0165-http-3-framing-sends-nghttp3s-default-settings-and-refuses-pushes-as-a-client-that-never-sends-max-push-id.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-730 — Read and write HTTP/3 frames, control streams and QPACK streams

## Goal

`Curl.Http3.UnitLibrary` reads and writes HTTP/3 frames (DATA, HEADERS, CANCEL_PUSH, SETTINGS, PUSH_PROMISE, GOAWAY, MAX_PUSH_ID, and reserved/grease types skipped), opens the client's control stream with the SETTINGS curl's build sends and its QPACK encoder and decoder streams, reads the server's control and QPACK streams, and turns protocol errors into the RFC 9114 error codes.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-729. The client's SETTINGS values come from BL-718's ADR (measured from curl's official build). Streams are byte streams handed in by the caller (the QUIC streams arrive through BL-721's contracts in `Curl.Protocol.Http.UnitLibrary`), so this library references no QUIC code.
- References: RFC 9114 sections 6 (stream mapping, unidirectional stream types), 7 (frames), 8 (error codes), 9 (extensions and grease).

## Acceptance criteria

- [x] `Curl.Http3.UnitTests` pin the client control stream's first bytes (stream type and SETTINGS) as measured, round-trip every frame type, skip grease frames and settings, and raise `H3_FRAME_UNEXPECTED`, `H3_MISSING_SETTINGS`, `H3_CLOSED_CRITICAL_STREAM` and `H3_FRAME_ERROR` for the RFC's cases.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered in `Curl.Http3.UnitLibrary`: `Http3Frame` and seven frame classes (`ToBytes`),
  `Http3FrameReader` (skips unknown and grease types; HTTP/2 types are `H3_FRAME_UNEXPECTED`),
  `Http3LocalUnidirectionalStreams` (control stream with `CurlSettings`, QPACK encoder and
  decoder stream types), `Http3PeerUnidirectionalStreams`, `Http3ControlStreamReader`,
  `Http3PeerQpackStreams`, `Http3Exception` with `Http3ErrorCode` (RFC 9114 section 8.1).
- "As measured": HTTP/3 cannot be recorded here. The Windows `curl.exe` (Schannel build) has
  no HTTP/3, and ADR-0144 already set that BL-730 checks the SETTINGS against nghttp3's source.
  The pinned bytes are `00 04 0d 06 ffffffffffffffff 01 00 07 00`: nghttp3's
  `nghttp3_stream_write_settings` for its defaults, which curl leaves alone.
- Design choices (push refused with `H3_ID_ERROR`, 64 KiB control-frame limit, grease
  settings dropped on read, any control-stream close is `H3_CLOSED_CRITICAL_STREAM`)
  are recorded in ADR-0165, decided by Claude under Stewart's delegation.
- Touches widened to the new ADR-0165 file: a design decision needs an ADR, and no task in
  Doing names `Documentation/Planning/Decisions`.
- Heads-up for BL-731: `Http3FrameReader` reads whole payloads up to its limit, so the
  request stream must set a limit that real `DATA` frames fit, or read them in pieces.
- Measured: `Curl.Http3.UnitLibrary` 100% line, 100% branch, 154 members, 0 failing, worst CRAP 10.
  184 tests in `Curl.Http3.UnitTests` pass.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Http3 reads and writes every HTTP/3 frame, opens curl's control and QPACK streams, and reads the server's with RFC 9114 error codes
