---
id: BL-5922
title: Reset each unfinished HTTP/2 stream with STREAM_CLOSED when a transfer is aborted, as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/Http2StreamAbortTests.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5922 — Reset each unfinished HTTP/2 stream with STREAM_CLOSED when a transfer is aborted, as curl does

## Goal

A transfer aborted while its HTTP/2 stream is unfinished (for example by `-m`) sends `RST_STREAM` with STREAM_CLOSED on that stream before the closing `GOAWAY`, as curl does.

## Context

Found in BL-5906. `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H :status: 401|www-authenticate: Digest realm="x", nonce="abc", qop="auth"|content-length: 3','D! no\n'` with `--http2-prior-knowledge --digest -u tester:secret -m 3`: the server never answers stream 3. curl 8.18.0 (nghttp2) sends `RST_STREAM stream 3 error 5`, then `GOAWAY ... shutdown`, and exits 28; Curl sends only the `GOAWAY`. With the 401's DATA left out, curl likewise resets stream 1 at the timeout. See `Http2Session.ShutDownAsync` and `Http2StreamConnection`.

## Acceptance criteria

- [ ] A unit test on the frames written to a test-double connection shows a transfer cancelled while its stream is unfinished writes `RST_STREAM` STREAM_CLOSED for it before `GOAWAY`.
- [ ] A transfer whose stream ended writes no `RST_STREAM`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: b98bd278 cc79feb6 3d836d5e.
