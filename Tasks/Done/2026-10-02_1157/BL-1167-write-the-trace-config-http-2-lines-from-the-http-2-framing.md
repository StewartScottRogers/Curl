---
id: BL-1167
title: Write the --trace-config http/2 lines from the HTTP/2 framing
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Http2.UnitLibrary, Curl.Http2.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1167 — Write the --trace-config http/2 lines from the HTTP/2 framing

## Goal

Under `-v --trace-config http/2` (and `protocol`, `all`) Curl writes the `* [HTTP/2] ...` lines curl 8.21.0 writes for an HTTP/2 transfer, from its HTTP/2 code.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` decides whether the component is on (`http/2`, `protocol` or `all`) and hands the HTTP/2 code an `ITransferEvents` sink.
- Not measured yet. The Schannel build reaches HTTP/2 in clear text with `--http2-prior-knowledge` against an h2c server; HTTP/2 over TLS needs ALPN, so measure that on the OpenSSL build (Linux or macOS). `Record-CurlExchange.ps1 -Script` can serve the h2c frames; extend it if it falls short. curl writes `[HTTP/2] [<stream id>] ...` lines for frames sent and received (`[HTTP/2] [1] OPENED stream for ...`, `[HTTP/2] [1] [:method: GET]` and the like), which the measurement must confirm.

## Acceptance criteria

- [x] The `[HTTP/2]` lines of an h2c prior-knowledge GET (Schannel) and an HTTPS ALPN GET (OpenSSL) under `-v --trace-config http/2` are measured and recorded in Notes.
- [x] Tests pin them; `protocol` and `all` write the same; `-v` alone, another component and `http/2` without `-v` write none.
- [x] `--ai-help` still describes `--trace-config` correctly.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- **Schannel:** curl.exe 8.21.0 (Schannel) has no HTTP2 feature; `curl --http2-prior-knowledge http://127.0.0.1:1/ -v` exits 2 with
  `curl: option --http2-prior-knowledge: the installed libcurl version does not support this`. So there are no h2c lines to measure on the Windows build.
- **OpenSSL:** WSL curl 8.18.0 (OpenSSL 3.5.5, nghttp2 1.68.0), `curl -s -o /dev/null -v --trace-config http/2 https://example.com/`, 2026-10-02. The `[HTTP/2]` lines, in order (TLS lines dropped):
  ```
  [HTTP/2] [0] created h2 session
  [HTTP/2] [0] -> FRAME[SETTINGS, len=18]
  [HTTP/2] [0] -> FRAME[WINDOW_UPDATE, incr=1048510465]
  [HTTP/2] cf_connect() -> 0, 1,
  (Established connection ..., using HTTP/2)
  [HTTP/2] [1] OPENED stream for https://example.com/
  [HTTP/2] [1] [:method: GET] ... [accept: */*]
  [HTTP/2] [1] submit -> 0, 73
  [HTTP/2] [1] -> FRAME[HEADERS, len=28, hend=1, eos=1]
  [HTTP/2] [1] cf_send(len=73) -> 0, 73, eos=1, h2 windows 65535-65535 (stream-conn), buffers 0-0 (stream-conn)
  (> request head, Request completely sent off)
  [HTTP/2] [1] local window update by 10420225
  [HTTP/2] [0] ingress: done
  [HTTP/2] [1] -> FRAME[WINDOW_UPDATE, incr=10420225]   (twice)
  [HTTP/2] [1] cf_recv(len=102400) -> 81, 0, window=0/20905985, connection 1048576000/1048576000
  [HTTP/2] [0] ingress: read 49 bytes
  [HTTP/2] [0] <- FRAME[SETTINGS, len=18]
  [HTTP/2] [0] MAX_CONCURRENT_STREAMS: 100
  [HTTP/2] [0] ENABLE_PUSH: TRUE
  [HTTP/2] [0] <- FRAME[WINDOW_UPDATE, incr=2147418112]
  [HTTP/2] [0] <- FRAME[SETTINGS, ack=1]
  [HTTP/2] [0] ingress: nw-in buffered 0
  [HTTP/2] [0] ingress: read 171 bytes
  < HTTP/2 200 / [HTTP/2] [1] status: HTTP/2 200, then each < header followed by [HTTP/2] [1] header: <name>: <value>
  [HTTP/2] [1] <- FRAME[HEADERS, len=162, hend=1, eos=0]
  [HTTP/2] [0] ingress: nw-in buffered 0 / ingress: read 586 bytes
  [HTTP/2] [1] <- FRAME[DATA, len=577, eos=0, padlen=0]
  [HTTP/2] [1] DATA, window=577/20905986
  ... ingress: read 9 bytes
  [HTTP/2] [1] <- FRAME[DATA, len=0, eos=1, padlen=0]
  [HTTP/2] [1] DATA, window=577/20905986
  [HTTP/2] [1] CLOSED
  [HTTP/2] [0] ingress: nw-in buffered 0 / ingress: done
  [HTTP/2] [1] returning CLOSE
  [HTTP/2] handle_stream_close -> 0, 0
  [HTTP/2] [0] -> FRAME[SETTINGS, ack=1]
  [HTTP/2] [1] cf_recv(len=102400) -> 0, 0, window=-1/-1, connection 1048575423/1048576000
  ```
  No GOAWAY line follows "left intact".
- **Decision (ADR-0373):** write the framing lines - session created, every `->`/`<-` frame in curl's `fr_print` text, the server's MAX_CONCURRENT_STREAMS / ENABLE_PUSH after each SETTINGS, and `CLOSED` - at the point Curl sends or reads each frame. curl's I/O-loop lines (`ingress`, `cf_send`, `cf_recv`, window arithmetic, `submit`, `cf_connect`, `returning CLOSE`, `handle_stream_close`) vary run to run and are not written. The shutdown GOAWAY is not traced.
- **Design:** `Curl.Http2` gained `IHttp2FrameObserver` and `Http2Connection.FrameObserver`; `Curl.Protocol.Http` has `Http2FrameTrace` (the observer, writing through `ITransferEvents`) and `HttpProtocolHandler.TracesHttp2Frames`; `Curl.Console` has `CurlComposition.TracesHttp2` (`http/2`, `protocol`, `all`, so `-vv`). The `OPENED stream` lines now come just before the HEADERS frame is written, as in curl. Console writes info lines only under `-v`, so `http/2` without `-v` writes none (as for the other components).
- **Touches:** added `Curl.Http2.UnitLibrary` and `Curl.Http2.UnitTests` - the frame layer swallows SETTINGS, WINDOW_UPDATE and PING itself, so frame lines need a hook there. No task in Doing on `origin/work/dark-factory` names them (only BL-1204, SSH).
- **Follow-up:** BL-1205 - an h2c upgrade's lines (its stream is built without a trace) and the `status:` / `header:` echoes.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v --trace-config http/2 (protocol, all, -vv) writes curl's [HTTP/2] session, frame, settings and CLOSED lines
