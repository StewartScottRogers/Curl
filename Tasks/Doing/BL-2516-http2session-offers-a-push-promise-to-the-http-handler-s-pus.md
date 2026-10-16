---
id: BL-2516
title: Http2Session offers a PUSH_PROMISE to the HTTP handler's push sink
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2515]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2516 — Http2Session offers a PUSH_PROMISE to the HTTP handler's push sink

## Goal

An `HttpProtocolHandler` given a push sink runs its HTTP/2 sessions with push enabled (BL-2515): a server's `PUSH_PROMISE` on a transfer's stream reaches the sink with the promise's headers as `name: value` lines (pseudo-headers included, in order, as curl's `h2_pushed_hdr` list) and a readable pushed response; a refused push is reset with `RST_STREAM` `CANCEL`; a handler with no sink keeps advertising `SETTINGS_ENABLE_PUSH` 0 as now.

## Context

Split out of BL-2469. Follow curl 8.22.0's `lib/http2.c` (`push_promise`, `on_frame_recv`, `on_begin_headers`). The sink comes in through the handler's constructor (as the RTSP handler's interleave sink and FTP's wildcard hooks do, built per transfer by `Curl.Engine`'s `EngineComposition.CreateProtocolHandlers`), so `Curl.Protocol.Abstractions` does not change. Suggested shape: an `IHttp2PushSink` in `Curl.Protocol.Http` with one synchronous method taking the header lines and an `IHttpPushedResponse` (the pushed stream presented as `Http2StreamConnection` presents a response: `HTTP/2 200 \r\n`, the headers, the empty line, then the DATA, readable as a `Stream`) and answering accept or refuse. `Http2Session` holds its frame lock while routing, so the refusal's RST_STREAM is written with `Frames.ResetStreamAsync` directly, not `ResetStreamAsync`. The promise's header block must be HPACK-decoded even when refused. A push on a stream whose transfer has no sink is refused.

## Acceptance criteria

- [ ] `Curl.Protocol.Http.UnitTests` pins a `PUSH_PROMISE` handed to a push sink with its headers, over a scripted connection, and the pushed response's bytes read through what the sink was given.
- [ ] `Curl.Protocol.Http.UnitTests` pins a refused push answered with `RST_STREAM` `CANCEL`, the parent's response still read whole.
- [ ] `Curl.Protocol.Http.UnitTests` pins the preface's ENABLE_PUSH 1 with a sink and 0 without (the existing pinned bytes unchanged).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 91fd7663 3fd2c7ef d0b60817 4272a6b9 3bf8be13.
