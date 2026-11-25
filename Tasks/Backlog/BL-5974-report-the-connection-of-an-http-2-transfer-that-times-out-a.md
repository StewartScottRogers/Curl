---
id: BL-5974
title: Report the connection of an HTTP/2 transfer that times out as left intact, as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionEnd.cs, Curl.Original.Protocol.Http.UnitTests/Http2ContinueWaitStatusTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5974 — Report the connection of an HTTP/2 transfer that times out as left intact, as curl does

## Goal

An HTTP/2 transfer that ends by `-m` reports its connection in `-v` as curl does.

## Context

Found by BL-5970. curl 8.18.0 nghttp2 build over `--http2-prior-knowledge -m 3` against a stream the server never ends writes `Connection #0 to host ... left intact`; Curl writes `closing connection #0`. See `HttpProtocolHandler.ConnectionEnd.cs`.

## Acceptance criteria

- [ ] For a timed-out HTTP/2 transfer Curl writes the measured connection line, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
