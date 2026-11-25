---
id: BL-5975
title: Reset an HTTP/2 stream with STREAM_CLOSED when -f fails a response that ended it with the upload unsent
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Http2ContinueWaitStatusTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5975 — Reset an HTTP/2 stream with STREAM_CLOSED when -f fails a response that ended it with the upload unsent

## Goal

`-f` on a status that ended an HTTP/2 stream during the wait for `100 Continue` resets the stream as curl does.

## Context

Found by BL-5972 (ADR-1404). Measured, curl 8.18.0 nghttp2 build: `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H! :status: 500|content-length: 0'`, `--http2-prior-knowledge -sS -v -f -H "Expect: 100-continue" -d x=1`: exit 22, `Connection #0 ... left intact`, then `RST_STREAM` stream 1 error 5 (STREAM_CLOSED) before GOAWAY. Curl sends no RST_STREAM: `-f` fails the exchange before `FinishSendingBodyAsync` resets the stream, and `Http2Session.ResetUnfinishedStreamsAsync` resets only streams whose response is unfinished.

## Acceptance criteria

- [ ] The measured `-f` case sends one `RST_STREAM` STREAM_CLOSED on stream 1 before GOAWAY, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
