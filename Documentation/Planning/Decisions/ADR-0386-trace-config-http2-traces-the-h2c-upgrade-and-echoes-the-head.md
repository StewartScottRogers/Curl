# ADR-0386 — `--trace-config http/2` traces the h2c upgrade and echoes each response head line

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1205.

## Context

ADR-0373 made `-v --trace-config http/2` write curl's HTTP/2 framing lines for prior-knowledge
and ALPN connections. An h2c upgrade (`--http2` over cleartext) wrote none, and no transfer
wrote the `status:` and `header:` lines curl's nghttp2 layer echoes as it writes the response
head. Measured with curl 8.18.0's OpenSSL build (nghttp2 1.68.0) in WSL through
`Record-CurlExchange.ps1` against a `101` followed by an empty SETTINGS, its acknowledgement,
`:status 200`, `content-length: 2` and `hi` on stream 1 (BL-1205 Notes), curl writes after
`Received 101, Switching to HTTP/2`:

```
[HTTP/2] added
[HTTP/2] upgrading connection to HTTP/2
Copied HTTP/2 data in stream buffer to connection buffer after upgrade: len=42
[HTTP/2] created session via Upgrade
[HTTP/2] [0] created h2 session (via h1 upgrade)
```

then the same frame lines as any HTTP/2 session, and after each `<` line of an HTTP/2 head
`[HTTP/2] [1] status: HTTP/2 200` or `[HTTP/2] [1] header: content-length: 2`.

## Decision

1. An h2c upgrade under `http/2` tracing writes the four upgrade lines above, in that order
   around the `Copied` line, and then stream 1's frame lines through the same
   `Http2FrameTrace` as ADR-0373's. The SETTINGS curl sends before the first stream opened
   after the upgrade is traced as `-> FRAME[SETTINGS, len=6]`.
2. Each line of an HTTP/2 response head, reported as a `<` line, is echoed right after it:
   the status line as `status: HTTP/2 <code>`, each header as `header: <name>: <value>`, the
   empty line not at all. The echo is driven from `HttpResponseHeadReader` (where the `<` lines
   are written), not from the frame layer.
3. As in ADR-0373, curl's I/O-loop lines are not written: `cf_connect`, `local window update`,
   `Process N bytes in connection buffer`, `notify MAX_CONCURRENT_STREAMS`, `DATA, window=`,
   `ingress`, `returning CLOSE`, `handle_stream_close`, `cf_recv`.

## Consequences

- The relative order of frame lines and head lines is Curl's, not nghttp2's: Curl reads the
  HEADERS frame (and writes its `<- FRAME[HEADERS ...]` line) before the head reader writes the
  `<` lines, where curl writes the head first; and Curl acknowledges the server's SETTINGS when
  it reads it, where curl's acknowledgement line comes after the stream closes. Every line
  curl writes for the framing is present; only their interleaving differs.
- HTTP/3 heads are not echoed: curl's `http/3` component writes its own lines (ADR-0375,
  BL-1168).

## Alternatives considered

- **Echo from the frame layer, as the header block is decoded.** Lost: the echoes would come
  before the `<` lines they follow in curl, and the frame layer does not know the `<` text.
- **Write the I/O-loop lines too.** Lost for the reason ADR-0373 gives: their count and values
  depend on curl's buffering, not on the exchange.
