---
id: BL-5938
title: Close an HTTP/2 connection with GOAWAY PROTOCOL_ERROR when DATA follows the head of a -I request, as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Http2HeadResponseWithDataTests.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5938 — Close an HTTP/2 connection with GOAWAY PROTOCOL_ERROR when DATA follows the head of a -I request, as curl does

## Goal

A `-I` request over HTTP/2 whose head is followed by a DATA frame closes the connection with `GOAWAY` PROTOCOL_ERROR, as curl does.

## Context

Found in BL-5922. `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H :status: 200|content-length: 3','D no\n'` with `--http2-prior-knowledge -sS -I -m 3`: curl 8.18.0 (nghttp2) exits 0 and closes with `GOAWAY last stream 0 error 1`, no debug data, and no `RST_STREAM`; Curl exits 0 and closes with `GOAWAY` error 0, debug `shutdown`. See `Http2StreamConnection.EndsAtFinalHead` (BL-5391, BL-5468) and `Http2Session.ShutDownAsync`.

## Acceptance criteria

- [ ] A unit test on the frames written to a test-double connection shows a `-I` transfer whose head is followed by DATA writes `GOAWAY` PROTOCOL_ERROR with no debug data, and no `GOAWAY` `shutdown`.
- [ ] The transfer still exits 0.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
