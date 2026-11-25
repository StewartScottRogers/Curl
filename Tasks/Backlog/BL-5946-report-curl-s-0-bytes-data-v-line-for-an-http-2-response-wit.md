---
id: BL-5946
title: Report curl's { [0 bytes data] -v line for an HTTP/2 response with no body
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpStreamOpenedLines.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/Http2EmptyResponseDataLineTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5946 — Report curl's { [0 bytes data] -v line for an HTTP/2 response with no body

## Goal

`curl -v` over HTTP/2 writes `{ [0 bytes data]` where curl does for a response that carries no body.

## Context

Measured in BL-5941 with `Record-CurlExchange.ps1 -ListenAddress <WSL gateway> -Curl wsl.exe` (curl 8.18.0, nghttp2), `-sS -v` `--http2-prior-knowledge`: curl writes `{ [0 bytes data]` for `-I` with the head ending the stream (`-Http2Frames 'H! :status: 200|content-length: 3'`, also status 204), for `-I` with a trailer ending it (after the `< x-t: v` line), and for a GET whose `content-length: 0` response ends on its HEADERS or on an empty DATA. Curl writes none. It writes none on a followed redirect's ignored hop. BL-5941 already reports the line when a `-I` response ends at a DATA frame (`HttpStreamOpenedLines.UnwantedDataReceived`); do not report it twice.

## Acceptance criteria

- [ ] Unit tests on the transfer's recorded events show one `{` data event, after any trailer line, for each of the four measured cases, and none for an ignored redirect body.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
