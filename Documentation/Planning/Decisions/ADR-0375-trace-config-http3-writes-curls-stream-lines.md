# ADR-0375 — `--trace-config http/3` writes curl's HTTP/3 stream lines, not its I/O loop lines

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1168.

## Context

Under `-v --trace-config http/3` (and `protocol`, `all`, so `-vv`) curl's ngtcp2/nghttp3 layer
writes `* [HTTP/3] ...` lines beside the `-v` output. Measured with curl.se's Windows build
8.18.0 (LibreSSL, ngtcp2 1.21.0, nghttp3 1.15.0) against `https://cloudflare-quic.com/`
(BL-1168 Notes), a 611-line trace of a GET, they are of three kinds:

- stream lines: `[0] end_headers, status=200` after the head's `<` lines, `[0] DATA len=<n>` and
  `[0] ACK <n>/<n> bytes of DATA` after each `{ [n bytes data]`, and `[0] CLOSED` and
  `[0] quic close(app_error=256) -> 0` as the response ends;
- connection lines: the handshake's time and the server's transport parameters, `peer verified`,
  `connect -> 0, done=1`, the idle timeout and keep-alive, the stream limit, and at the end
  `easy handle is done`, `no active streams, unset keep-alive` and the pool's
  `query conn[0]: MAX_CONCURRENT`; plus the per-header `status:` / `header:` echoes;
- lines about curl's own I/O loop: `ingress, recvfrom -> EAGAIN`, `egress, collect and send
  packets`, `vquic_send(...)`, `vquic_recvfrom(...)`, `cf_send(...)`, `cf_recv(...)` and
  `read_stream(...)`, hundreds of them, whose count follows how packets happen to arrive.

The Schannel build curl.exe 8.21.0 on Windows has no HTTP/3, so there is no 8.21.0 trace to
measure on this machine; 8.18.0 is the reference ADR-0144 names for HTTP/3.

## Decision

1. `Http3StreamTrace` (in `Curl.Protocol.Http`) writes the stream lines through
   `ITransferEvents.ReportInfo`. `Http3StreamConnection` tells it of each response head (interim
   heads too, as nghttp3's `end_headers` callback fires for each), each piece of `DATA` it
   reads and the stream's end.
2. curl writes each stream line after handing what it describes to the transfer, so the trace
   holds its lines until the stream is next read: `end_headers` lands after the head's `<`
   lines, `DATA len` and `ACK` after the `{` line of that piece.
3. One `DATA len` / `ACK` pair is written per piece of body the stream reads, as curl writes
   one per piece nghttp3 hands it; the pieces follow packetization in both.
4. The I/O loop lines are not written, as ADR-0373 decided for HTTP/2: they describe curl's
   buffering, not the exchange. The connection lines and the header echoes are BL-1208.
5. `Curl.Console` turns it on for `http/3`, `protocol` or `all` (`CurlComposition.TracesHttp3`),
   through `HttpProtocolHandler.TracesHttp3Streams`, separate from `TracesHttp2Frames`, so each
   component turns on only its own version's lines.

## Consequences

- A trace of an HTTP/3 transfer carries curl's stream lines in curl's places; it lacks the
  connection lines and header echoes until BL-1208, and the I/O loop lines for good.
- The task asked for the lines from `Curl.Http3.UnitLibrary`; that library holds only the
  HTTP/3 frames and QPACK, and the stream the lines describe is `Http3StreamConnection` in
  `Curl.Protocol.Http.UnitLibrary`, so the trace lives there, beside `Http2FrameTrace`.

## Alternatives considered

- Report each line the moment the frame is read: simpler, but `end_headers` would precede the
  `<` lines and `DATA len` the `{` line, the reverse of curl's order.
- Imitate the I/O loop lines: their counts and values could only be invented, not reproduced.
