---
id: BL-5946
title: Report curl's { [0 bytes data] -v line for an HTTP/2 response with no body
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpStreamOpenedLines.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseBody.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.IgnoredBody.cs, Curl.Original.Protocol.Http.UnitTests/Http2EmptyResponseDataLineTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5946 — Report curl's { [0 bytes data] -v line for an HTTP/2 response with no body

## Goal

`curl -v` over HTTP/2 writes `{ [0 bytes data]` where curl does for a response that carries no body.

## Context

Measured in BL-5941 with `Record-CurlExchange.ps1 -ListenAddress <WSL gateway> -Curl wsl.exe` (curl 8.18.0, nghttp2), `-sS -v` `--http2-prior-knowledge`: curl writes `{ [0 bytes data]` for `-I` with the head ending the stream (`-Http2Frames 'H! :status: 200|content-length: 3'`, also status 204), for `-I` with a trailer ending it (after the `< x-t: v` line), and for a GET whose `content-length: 0` response ends on its HEADERS or on an empty DATA. Curl writes none. It writes none on a followed redirect's ignored hop. BL-5941 already reports the line when a `-I` response ends at a DATA frame (`HttpStreamOpenedLines.UnwantedDataReceived`); do not report it twice.

## Acceptance criteria

- [x] Unit tests on the transfer's recorded events show one `{` data event, after any trailer line, for each of the four measured cases, and none for an ignored redirect body.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `Http2StreamConnection.EndedWithEmptyData` records that END_STREAM came on a HEADERS frame (head or trailer), or on a DATA frame of a response with no body bytes; `ReportEmptyStreamData` in `HttpProtocolHandler.ResponseBody.cs` reports the empty data after the trailers. An ignored body never reaches it, and a `-I` response ended at a DATA frame does not set the property, so nothing is reported twice.
- Measured beyond the four cases, curl 8.18.0 (nghttp2, WSL), `-sS -v`, and matched because curl does the same: a GET 204 or 304 ending on HEADERS or empty DATA, and `content-length: 0` or `content-length: 3` with data ended by a trailer, each write `{ [0 bytes data]` (after the trailer). Data ended by an empty DATA writes only `{ [3 bytes data]` on `-v`, so no event is added there.
- No `content-length`, no body, ended by a trailer: curl writes the line after the trailer; Curl writes it before, from the body reader, whose file BL-5948 held. Filed as BL-5956.
- `HttpStreamOpenedLines.cs` and `HttpProtocolHandler.IgnoredBody.cs` needed no change.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`, 0 failing members. Built curl against the recorder (data then trailer) writes the same lines as real curl.
- CI on Linux and macOS: the change and its tests use no platform-dependent behaviour.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 64992237 3d836d5e.
- 2026-11-25: Doing -> Done. Curl reports curl's empty data line for an HTTP/2 response with no body or ended by a trailer
