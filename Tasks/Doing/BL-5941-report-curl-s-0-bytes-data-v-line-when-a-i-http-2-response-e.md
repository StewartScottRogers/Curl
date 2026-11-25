---
id: BL-5941
title: Report curl's { [0 bytes data] -v line when a -I HTTP/2 response ends at a DATA frame
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Http2HeadResponseWithDataTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5941 — Report curl's { [0 bytes data] -v line when a -I HTTP/2 response ends at a DATA frame

## Goal

`curl -v -I` over HTTP/2, answered with a head and then a DATA frame, writes the same `-v` data lines as curl.

## Context

Found in BL-5938. `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H :status: 200|content-length: 3','D no\n'` with `--http2-prior-knowledge -sS -v -I -m 3`: curl 8.18.0 (nghttp2, run through WSL) writes `{ [0 bytes data]` after `* nghttp2 shuts down connection with error 1: PROTOCOL_ERROR` and before `* shutting down connection #0`; Curl writes no such line. curl writes it too when the DATA is empty and ends the stream (`'D! '`). Measure `-I` over HTTP/1.1 and with stderr to a terminal before deciding when it appears. See `Http2StreamConnection.EndAtUnwantedBodyAsync`.

## Acceptance criteria

- [ ] A unit test on the transfer's recorded events shows the `-v` data line curl writes for both measured cases.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 54860144 3d836d5e.
