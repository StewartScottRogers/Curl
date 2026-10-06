---
id: BL-1205
title: Write the --trace-config http/2 lines of an h2c upgrade and the status and header echoes
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1167]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1205 — Write the --trace-config http/2 lines of an h2c upgrade and the status and header echoes

## Goal

Under `-v --trace-config http/2` an h2c-upgraded transfer writes the `[HTTP/2]` frame lines too, and every HTTP/2 response writes curl's `[HTTP/2] [1] status: HTTP/2 200` and `[HTTP/2] [1] header: name: value` echo after each `<` line, as curl 8.21.0 does.

## Context

- Follow-up of BL-1167 (ADR-0373). `Http2FrameTrace` writes the frame lines for prior-knowledge and ALPN streams; `HttpH2cUpgradeConnection` builds its `Http2StreamConnection` without a trace, so an h2c upgrade writes none.
- The `status:` / `header:` echoes (measured in BL-1167's Notes) interleave with the `<` header lines the handler writes, so they belong where the response head is written, not in the frame layer.
- Measure the h2c upgrade's lines with an OpenSSL build (WSL); the Schannel build has no HTTP/2.

## Acceptance criteria

- [x] An h2c upgrade under `-v --trace-config http/2` writes the measured `[HTTP/2]` lines; tests pin them.
- [x] The `status:` and `header:` echoes are measured and pinned, after each `<` line.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- **Measured:** WSL curl 8.18.0 (OpenSSL 3.5.5, nghttp2 1.68.0) through `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress <WSL host IP>`,
  `curl -s -o /dev/null -v --http2 --trace-config http/2 http://<host>:18123/x`, 2026-10-02, against a 101 head followed in one write by an
  empty SETTINGS, its ack, HEADERS `88 5c 01 32` (`:status 200`, `content-length: 2`) and DATA `hi` eos on stream 1. After `Received 101`:
  ```
  [HTTP/2] added
  [HTTP/2] upgrading connection to HTTP/2
  Copied HTTP/2 data in stream buffer to connection buffer after upgrade: len=42
  [HTTP/2] created session via Upgrade
  [HTTP/2] [0] created h2 session (via h1 upgrade)
  [HTTP/2] [0] -> FRAME[SETTINGS, len=18]
  [HTTP/2] [0] -> FRAME[WINDOW_UPDATE, incr=1048510465]
  [HTTP/2] cf_connect() -> 0, 1,
  [HTTP/2] [1] local window update by 10420225
  [HTTP/2] Process 42 bytes in connection buffer
  [HTTP/2] [0] <- FRAME[SETTINGS, len=0]
  [HTTP/2] [0] MAX_CONCURRENT_STREAMS: -1
  [HTTP/2] [0] ENABLE_PUSH: TRUE
  [HTTP/2] [0] notify MAX_CONCURRENT_STREAMS: 4294967295
  [HTTP/2] [0] <- FRAME[SETTINGS, ack=1]
  < HTTP/2 200
  [HTTP/2] [1] status: HTTP/2 200
  < content-length: 2
  [HTTP/2] [1] header: content-length: 2
  [HTTP/2] [1] <- FRAME[HEADERS, len=4, hend=1, eos=0]
  <
  [HTTP/2] [1] <- FRAME[DATA, len=2, eos=1, padlen=0]
  [HTTP/2] [1] DATA, window=2/20905986
  [HTTP/2] [1] CLOSED
  ... ingress: done, returning CLOSE, handle_stream_close
  [HTTP/2] [0] -> FRAME[SETTINGS, ack=1]
  ... cf_recv(...)
  ```
- **Decision (ADR-0386):** write the four upgrade lines and stream 1's frame lines; echo each HTTP/2 head line after its `<` line
  (`status: HTTP/2 <code>`, `header: name: value`, nothing for the empty line); I/O-loop lines (`cf_connect`, `local window update`,
  `Process N bytes`, `notify MAX_CONCURRENT_STREAMS`, `DATA, window=`, ...) stay out as in ADR-0373. The post-upgrade SETTINGS before
  stream 3 is traced as `-> FRAME[SETTINGS, len=6]`. Curl writes the `<- FRAME[HEADERS]` line before the head and the SETTINGS ack
  when it reads the SETTINGS; curl interleaves those two differently (recorded as a consequence in the ADR).
- **Design:** `HttpH2cUpgradeConnection` takes the trace's events (`TracesHttp2Frames`), builds stream 1's `Http2FrameTrace`, and
  `Http2Session.StartUpgradedStreamAsync` points the frame observer at it before the preface. `HttpResponseHeadReader.LineReported`
  fires after each `<` line; the handler hands it to the HTTP/2 stream (`Http2StreamConnection.EchoResponseLine`).
- **Tests:** `Http2FrameTraceTests` (upgrade lines, echoes), `HttpProtocolHandlerTests.Http2Trace` (h2c sequence pinned, post-upgrade
  SETTINGS on a 401 retry, prior-knowledge test now carries the echoes).

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v --trace-config http/2 traces an h2c upgrade's lines and echoes every HTTP/2 head line as status:/header: after its < line
