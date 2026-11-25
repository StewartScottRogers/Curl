---
id: BL-5938
title: Close an HTTP/2 connection with GOAWAY PROTOCOL_ERROR when DATA follows the head of a -I request, as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Http2HeadResponseWithDataTests.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpStreamOpenedLines.cs, Curl.Original.Protocol.Http.UnitTests/Http2StreamAbortTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-5938 — Close an HTTP/2 connection with GOAWAY PROTOCOL_ERROR when DATA follows the head of a -I request, as curl does

## Goal

A `-I` request over HTTP/2 whose head is followed by a DATA frame closes the connection with `GOAWAY` PROTOCOL_ERROR, as curl does.

## Context

Found in BL-5922. `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H :status: 200|content-length: 3','D no\n'` with `--http2-prior-knowledge -sS -I -m 3`: curl 8.18.0 (nghttp2) exits 0 and closes with `GOAWAY last stream 0 error 1`, no debug data, and no `RST_STREAM`; Curl exits 0 and closes with `GOAWAY` error 0, debug `shutdown`. See `Http2StreamConnection.EndsAtFinalHead` (BL-5391, BL-5468) and `Http2Session.ShutDownAsync`.

## Acceptance criteria

- [x] A unit test on the frames written to a test-double connection shows a `-I` transfer whose head is followed by DATA writes `GOAWAY` PROTOCOL_ERROR with no debug data, and no `GOAWAY` `shutdown`.
- [x] The transfer still exits 0.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured, curl 8.18.0 (nghttp2, WSL), `Record-CurlExchange.ps1 -Http2Origin prior-knowledge`: `-I` with non-empty DATA (with or without `content-length`) prints `* nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`, sends `GOAWAY` error 1, prints `* shutting down connection #0`, exits 0, and a second URL opens a new connection. Empty DATA ending the stream, and `-I -X GET` with DATA within `content-length`, leave the connection intact and close with `GOAWAY` `shutdown`; `-I -X GET` with DATA past it behaves as the first case.
- So the trigger is DATA past the promised length (none for HEAD), not any DATA (ADR-1384). `Http2Session.AcceptsNewStreams` is false once this client sent a `GOAWAY`. Curl's `-v` lines and `GOAWAY` matched curl's in an end-to-end run with two URLs.
- `touches` widened to `HttpStreamOpenedLines.cs` (the `-v` line) and `Http2StreamAbortTests.cs` (its BL-5922 `-I` test expected the `shutdown` GOAWAY); no task in Doing named either.
- Fast tests: 58045 passed, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: a36d99c1 03f53697 3d836d5e.
- 2026-11-25: Doing -> Done. A -I HTTP/2 response with DATA past its length closes with GOAWAY PROTOCOL_ERROR and exits 0, as curl does
