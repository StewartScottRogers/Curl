---
id: BL-3838
title: Close an HTTP/2 connection given up after five REFUSED_STREAM retries without the closing GOAWAY
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Protocol.Http.UnitTests/Cgr031GiveUpTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3838 — Close an HTTP/2 connection given up after five REFUSED_STREAM retries without the closing GOAWAY

## Goal

When an HTTP/2 server refuses the request every time, Curl closes the sixth connection (the one it gives up on with `Connection died, tried 5 times before giving up`) without sending a GOAWAY, as curl 8.18.0 with nghttp2 does. The five connections it retries from still send one.

## Context

Found by BL-3560 (CGR-031, ADR-0900). A scenario server that answers every HEADERS with RST_STREAM REFUSED_STREAM shows the difference: real curl 8.18.0 (WSL) sent GOAWAY (type 7, len 17) on connections 1 to 5 and none on 6, in both measurements, while Curl sends one on all six. In curl the given-up connection is closed prematurely, after `multi_done` with an error, so it gets no graceful shutdown. Curl's held `Http2Session` sends `ShutDownAsync`'s GOAWAY when the connection closes (`HttpProtocolHandler.ExchangeOnConnectionAsync`, `UnheldHttp2Session`; BL-817). The fix probably needs a way to close a session without the GOAWAY on the give-up path of `StreamRefusedOutcome`. The scenario server is a C# file-based app, h2srv.cs; its source is in BL-3560's Notes.

## Acceptance criteria

- [ ] A unit test pins that the handler, given six connections that each refuse stream 1 with REFUSED_STREAM, writes the closing GOAWAY on the first five and not on the sixth.
- [ ] Exit 56 with `Connection died, tried 5 times before giving up` and the five GOAWAYs on the retried connections are unchanged.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 664244f6 a4d0ee71 779790c0.
