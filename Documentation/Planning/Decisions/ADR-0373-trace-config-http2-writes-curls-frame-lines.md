# ADR-0373 — `--trace-config http/2` writes curl's HTTP/2 frame lines, not its buffering lines

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1167.

## Context

Under `-v --trace-config http/2` (and `protocol`, `all`, so `-vv`) curl's nghttp2 layer writes
`* [HTTP/2] [<stream>] ...` lines beside the `-v` output. Measured with curl 8.18.0's OpenSSL
build (nghttp2 1.68.0) in WSL against `https://example.com/` (BL-1167 Notes), they are of two
kinds:

- lines driven by the framing: `[0] created h2 session`, `-> FRAME[...]` for every frame sent,
  `<- FRAME[...]` for every frame received (curl's `fr_print`), `[0] MAX_CONCURRENT_STREAMS: n`
  and `[0] ENABLE_PUSH: TRUE|false` after each SETTINGS the server sends, and `[1] CLOSED`;
- lines about curl's own I/O loop: `ingress: read N bytes`, `ingress: nw-in buffered 0`,
  `cf_send(...)`, `cf_recv(...)`, `local window update by`, `DATA, window=a/b`,
  `cf_connect() -> 0, 1,`, `submit -> 0, 73`, `returning CLOSE`, `handle_stream_close`, and
  the per-header `status:` / `header:` echoes. Their count and values follow how reads happen
  to split and how curl buffers, and differ from run to run.

The Schannel build curl.exe 8.21.0 on Windows has no HTTP/2 at all: `--http2-prior-knowledge`
is refused ("the installed libcurl version does not support this"), so there is nothing on the
Windows build to measure; Curl speaks HTTP/2 everywhere (ADR-0141).

## Decision

1. `Curl.Http2`'s `Http2Connection` takes an optional `IHttp2FrameObserver`, told of each frame
   after it is written and of each frame read before it is acted on.
2. `Http2FrameTrace` (in `Curl.Protocol.Http`) is that observer for a transfer and writes the
   framing lines through `ITransferEvents.ReportInfo`, in curl's text: `fr_print`'s description
   of each frame type, the server's concurrency and push settings after each SETTINGS, the
   session's creation before the preface, and `CLOSED` when a response ends. CONTINUATION
   frames are not named, as nghttp2 hands curl a header block whole.
3. The connection's frames are reported to the trace of the transfer that last opened a
   stream on the session; the GOAWAY sent when the session shuts down is not reported, as
   curl's transfer is over by then.
4. The `OPENED stream` lines move to just before the stream's HEADERS are written, so the
   frame line follows them as in curl.
5. The I/O loop lines in the second list are not written: they describe curl's buffering, not
   the exchange, and could only be imitated, not reproduced.
6. `Curl.Console` turns it on for `http/2`, `protocol` or `all` (`CurlComposition.TracesHttp2`),
   through `HttpProtocolHandler.TracesHttp2Frames`.

## Consequences

- A trace of an HTTP/2 transfer carries every frame line curl's does, in the order frames
  cross the wire; it lacks curl's buffering lines and its `status:` / `header:` echoes.
- Curl answers a server's SETTINGS as it reads it, so `-> FRAME[SETTINGS, ack=1]` comes right
  after the SETTINGS received; nghttp2 queues it and curl writes it later.
- HTTP/3 (`--trace-config http/3`) writes nothing yet.
