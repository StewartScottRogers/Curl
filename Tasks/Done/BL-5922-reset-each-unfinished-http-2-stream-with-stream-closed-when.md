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
completed: 2026-11-25
---
# BL-5922 — Reset each unfinished HTTP/2 stream with STREAM_CLOSED when a transfer is aborted, as curl does

## Goal

A transfer aborted while its HTTP/2 stream is unfinished (for example by `-m`) sends `RST_STREAM` with STREAM_CLOSED on that stream before the closing `GOAWAY`, as curl does.

## Context

Found in BL-5906. `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H :status: 401|www-authenticate: Digest realm="x", nonce="abc", qop="auth"|content-length: 3','D! no\n'` with `--http2-prior-knowledge --digest -u tester:secret -m 3`: the server never answers stream 3. curl 8.18.0 (nghttp2) sends `RST_STREAM stream 3 error 5`, then `GOAWAY ... shutdown`, and exits 28; Curl sends only the `GOAWAY`. With the 401's DATA left out, curl likewise resets stream 1 at the timeout. See `Http2Session.ShutDownAsync` and `Http2StreamConnection`.

## Acceptance criteria

- [x] A unit test on the frames written to a test-double connection shows a transfer cancelled while its stream is unfinished writes `RST_STREAM` STREAM_CLOSED for it before `GOAWAY`.
- [x] A transfer whose stream ended writes no `RST_STREAM`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `Http2Session.ShutDownAsync` resets every stream whose response is unfinished, lowest first, before its `GOAWAY` (ADR-1374). Both the `-m` path and the pool's close go through it. A stream the server reset is now dropped from the open streams when the reset is the reading stream's own.
- `HttpProtocolHandler`: a timed-out transfer on an h2c-upgraded connection now hands its session on, so it too is reset and shut down; before, it sent neither frame.
- Measured, curl 8.18.0 (nghttp2), `--http2-prior-knowledge -sS -m 3` against `Record-CurlExchange.ps1 -Http2Origin prior-knowledge`: a head `200` with no DATA (exit 28) and `-f` on a head `404` with no DATA (exit 22) both close with `RST_STREAM stream 1 error 5`, then `GOAWAY error 0 debug shutdown`; Curl's closing frames now match.
- Same setup with `-I` and a DATA frame after the head: curl sends no `RST_STREAM`, so a response `-I` ended is not reset. curl's `GOAWAY` there is PROTOCOL_ERROR, which Curl did not send before this task either: filed as BL-5935.
- Checks: `dotnet build` clean; fast tests passed in all 114 test projects; `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 100% line and branch, 0 failing members. Tests are in-memory, so platform-neutral.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: b98bd278 cc79feb6 3d836d5e.
- 2026-11-25: Doing -> Done. An aborted transfer's unfinished HTTP/2 stream is reset with STREAM_CLOSED before the closing GOAWAY
