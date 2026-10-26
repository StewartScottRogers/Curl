---
id: BL-3831
title: Print curl's -v line 'received GOAWAY, error=N, last_stream=N' when an HTTP/2 server sends GOAWAY
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Protocol.Http.UnitLibrary/HttpConnectionInfoLines.cs, Curl.Protocol.Http.UnitTests/Http2GoAwayInfoLineTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3831 — Print curl's -v line 'received GOAWAY, error=N, last_stream=N' when an HTTP/2 server sends GOAWAY

## Goal

When an HTTP/2 server sends GOAWAY, `curl -v` prints `* received GOAWAY, error=<code>, last_stream=<id>` as curl 8.18.0 with nghttp2 does, and Curl prints the same line at the same point.

## Context

Found by BL-3560 (CGR-031). Measured with real curl 8.18.0 (WSL) against a server that answers HEADERS with GOAWAY last=0 NO_ERROR: its `-v` shows `* Request completely sent off`, `* received GOAWAY, error=0, last_stream=0`, `* REFUSED_STREAM, retrying a fresh connect`. Curl prints every line except the GOAWAY one. The line comes from curl's `http2.c` `on_frame_recv` (an `infof`). `Http2Session.LogPeerConnectionFrames` already sees the GOAWAY (`connectionLog.GoAwayReceived`), but only for the diagnostic log. Check the text against curl 8.22.0 `lib/http2.c` before pinning it. Check too whether it is printed for a GOAWAY that has an error code.

## Acceptance criteria

- [ ] A unit test pins the `-v` line `received GOAWAY, error=0, last_stream=0` before `REFUSED_STREAM, retrying a fresh connect` for a GOAWAY that leaves stream 1 unprocessed.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
