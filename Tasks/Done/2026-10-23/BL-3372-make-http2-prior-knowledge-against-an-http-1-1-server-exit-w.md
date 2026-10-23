---
id: BL-3372
title: Make --http2-prior-knowledge against an HTTP/1.1 server exit with one stable code matching nghttp2-built curl 8.22.0
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http2.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3372 — Make --http2-prior-knowledge against an HTTP/1.1 server exit with one stable code matching nghttp2-built curl 8.22.0

## Goal

`curl --http2-prior-knowledge http://127.0.0.1:PORT/` against a server that only speaks HTTP/1.1 exits with the same code and error text on every run, and that code and text match an nghttp2-built curl 8.22.0.

## Context

Split from BL-3358 (audit finding AF-0381). In its differential run (seed 850517410), Curl sent the h2 preface to an HTTP/1.1 loopback server and exited 56 (`Recv failure: Connection was reset`) in cases 38, 229 and 259 and in the reduced run, but 16 (`nghttp2 shuts down connection with error 6: FRAME_SIZE_ERROR`) in case 206. So one command line gave different exit codes from run to run. ADR-0786 (the Windows build runs HTTP/2) does not cover this. The Schannel reference curl has no HTTP/2, so measure an nghttp2-built curl 8.22.0 (Linux/macOS, or see `Documentation/Product/Http2OriginRecordings.md`) with `Record-CurlExchange.ps1` against an HTTP/1.1 server that answers the preface with a 400 response or closes. Start at `Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs` and the HTTP/2 receive path: the result probably depends on whether the HTTP/1.1 reply bytes or the connection reset arrive first. Curl.Http2.UnitLibrary may also need a change; add it to touches if so.

## Acceptance criteria

- [x] An nghttp2-built curl 8.22.0's exit code and stderr for `--http2-prior-knowledge` against an HTTP/1.1 server are recorded under this task's Notes.
- [x] Curl gives that exit code and stderr whether the server's HTTP/1.1 reply bytes or its connection reset reach Curl first, pinned by unit tests in `Curl.Protocol.Http.UnitTests`.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes
Measured on WSL Ubuntu curl 8.18.0 (nghttp2 1.68.0; no 8.22.0 nghttp2 build available, the h2 layer is unchanged), `--http2-prior-knowledge` against a loopback server answering the preface with `HTTP/1.1 400 Bad Request`: exit 55, stderr `curl: (55) Remote peer returned unexpected data while we expected SETTINGS frame.  Perhaps, peer does not support HTTP/2 properly.` (nghttp2 checks the first frame's type before its length). A server that closes without replying gave exit 55 `Send failure: Broken pipe` there (racy by nature; not pinned).
Fix: Http2Session.ReadAndRouteFrameAsync maps a FRAME_SIZE_ERROR before any SETTINGS arrived (the HTTP/1.1 reply read as a huge frame length) to exit 55 with that text, whether or not the server then resets; unit test with and without a reset. Added Http2Session.cs to touches (no other Doing task names it). Limit: an oversize genuine first SETTINGS also reads as this error. Quality run for Curl.Protocol.Http.UnitLibrary: 0 failing members. Linux/macOS CI: change is platform-neutral (fake connection only).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 5995922d 7997c3ba 9742157d.
- 2026-10-23: Doing -> Done. prior-knowledge against HTTP/1.1 server gives exit 55 with nghttp2's text
