---
id: BL-5961
title: Read the status of an HTTP/2 or HTTP/3 response during the 100 Continue wait, not an assumed 200
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Original.Protocol.Http.UnitTests/HttpContinueWaitConnectionTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5961 — Read the status of an HTTP/2 or HTTP/3 response during the 100 Continue wait, not an assumed 200

## Goal

The wait for `100 Continue` on an HTTP/2 or HTTP/3 stream acts on the response's real status, as curl does.

## Context

Found by BL-5960. `HttpContinueWaitConnection.StatusCodeOf` parses the first line with `HttpStatusLine.Parse`, which gives an assumed 200 for the `HTTP/2 NNN ` and `HTTP/3 NNN ` lines the stream connections write (`HttpStatusLine.ParseHttp2OrHttp3` is the parser for those). So on a stream every status, a `100` or a `500` too, is waited out and the body is then sent. Measure curl 8.22.0 over HTTP/2 with `-H "Expect: 100-continue"` for a `100`, a `200` and a `500` (`Record-CurlExchange.ps1 -Http2`) before pinning anything. `HttpContinueWaitConnection.IsMultiplexedStream` marks the stream case.

## Acceptance criteria

- [ ] For a `100`, a `200` and a `500` that arrive on an HTTP/2 stream during the wait, Curl sends or withholds the body and writes the `-v` lines as curl 8.22.0 does, pinned by unit tests.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
