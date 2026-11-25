---
id: BL-5958
title: Write curl's nghttp2 shuts down -v line when HTTP/2 DATA or a header field makes a response malformed
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Cgr138Tests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5958 — Write curl's nghttp2 shuts down -v line when HTTP/2 DATA or a header field makes a response malformed

## Goal

`-v` shows curl's nghttp2 line before `* closing connection #0` for every HTTP/2 response Curl shuts the connection down for, not only for a head that ends the stream.

## Context

BL-5947 measured `* nghttp2 shuts down connection with error 1: PROTOCOL_ERROR` after the head lines (curl 8.18.0) and added it for a refused head only, through `HttpTransferException.InfoLines`. The other `ShutDownMalformedAsync` calls in `Http2StreamConnection` (DATA past or short of `content-length`, headers after the head without END_STREAM, an invalid header field, whose message differs) set no `InfoLines`. Measure each with `Record-CurlExchange.ps1 -Http2Frames` before pinning the lines and their order; a failure thrown from a body read may also need `HttpProtocolHandler.TransferFailures.cs` (add it to `touches`).

## Acceptance criteria

- [ ] A unit test pins the measured `-v` lines for DATA past and short of `content-length` and for an invalid header field.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
