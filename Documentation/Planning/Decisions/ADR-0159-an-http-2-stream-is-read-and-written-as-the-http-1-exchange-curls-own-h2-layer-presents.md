# ADR-0159 — An HTTP/2 stream is read and written as the HTTP/1 exchange curl's own HTTP/2 layer presents

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-658.

## Context

ADR-0141 decides that HTTP/2 is hand-built and spoken on every platform; BL-656 and
BL-657 built HPACK and the frame layer in `Curl.Http2.UnitLibrary`. BL-658 makes the HTTP
handler send a request and read its response on an HTTP/2 stream, and requires every
existing path - output, `-i`/`-D`, `-f`, redirects, authentication, cookies, progress - to
work for HTTP/2 as for HTTP/1.1, with the plumbing reusable for HTTP/3 (BL-731).

libcurl's own HTTP/2 layer (`lib/http2.c`) does not have a second HTTP code path. It turns
the HTTP/1 request its HTTP code formats into the HTTP/2 header list
(`Curl_http_req_to_h2`), and hands each response header block back to the HTTP/1 header
parser as the text `HTTP/2 200 \r\n`, `name: value\r\n` lines and an empty line; DATA goes
to the body writer as it comes.

Measured with curl.se's nghttp2 build (curl 8.18.0, whose HTTP/2 lines ADR-0141 found
byte-identical to the Linux build) and `Record-CurlExchange.ps1` serving hand-built frames
with `--http2-prior-knowledge` (BL-658 Notes):

- HEADERS: `:method`, `:scheme`, `:authority` (the `Host` value, a custom `Host`
  included), `:path`, then the HTTP/1 head's headers in order with lower-cased names, less
  `Host`, `Connection`, `Keep-Alive`, `Proxy-Connection`, `Transfer-Encoding` and
  `Upgrade`; `TE` only as `te: trailers`. No `Expect: 100-continue`, even for a 2 MB `-T`.
  A body goes in DATA frames, the last carrying END_STREAM; a request without one sets
  END_STREAM on HEADERS.
- Output: `-i` writes `HTTP/2 200 \r\n` (a blank after the code), each header as received,
  the empty line, the body; trailers follow the body as `name: value\r\n` lines with no
  empty line after them.
- Failures: RST_STREAM - exit 92, `HTTP/2 stream 1 was not closed cleanly: INTERNAL_ERROR
  (err 2)`; a protocol error - exit 16, `nghttp2 shuts down connection with error 1:
  PROTOCOL_ERROR`; GOAWAY with an error mid-body - exit 56, `Failure when receiving data
  from the peer`; the peer closing before the head - exit 16, `Error in the HTTP2 framing
  layer`; closing mid-body - exit 18, `Transferred a partial file`.

## Decision

1. **The handler keeps one exchange path.** `Http2StreamConnection` is an `IConnection`
   over one stream: its first write is the HTTP/1 request head, sent as HEADERS
   (`Http2RequestHeaders`); later writes are body DATA; its reads give each response head
   as curl's layer writes it (`Http2ResponseHead`), then the DATA, then zero at the end of
   the stream. The existing head reader, body reader, request writer and every decision
   built on them run unchanged over it. The head reader parses these status lines as
   version 2.0 (`HttpStatusLine.ParseHttp2`), so `%{http_version}` and the report say 2.
2. **When HTTP/2 is spoken:** with the new `HttpVersionPreference.Http2PriorKnowledge`
   (`--http2-prior-knowledge`, wired by BL-659), from the first byte; and when the connect
   reports `ConnectResult.ApplicationProtocol` `h2`, which the TLS provider sets from ALPN
   (BL-659). Both are new members of `Curl.Protocol.Abstractions.UnitLibrary`, the only
   seam through which the connector and the command line reach the handler.
3. **Framing:** an HTTP/2 request is framed by `HttpRequestFraming.ForHttp2` - never
   chunked, no `Expect` of curl's own, no wait for `100 Continue` - and a body of unknown
   length sends no `Content-Length` and ends with an empty END_STREAM DATA frame.
4. **Failures** map to the measured exits and messages above; a response head with no
   valid `:status`, or DATA before the head, is reset with PROTOCOL_ERROR and fails with
   exit 92, as nghttp2 treats a malformed response; an undecodable header block is exit 16
   with COMPRESSION_ERROR.
5. **An HTTP/2 connection is pooled with its session** (BL-817, as curl 8.18.0 pools it).
   Protocol libraries never reference `Curl.Networking`, so the session travels through
   `Curl.Protocol.Abstractions`: `IConnectionSession` (one member, `ShutDownAsync`) and two
   default members on `IConnection` - `Session` and `TryHoldSession`. The handler hands its
   `Http2Session` to the connection; a pooled connection (`PooledConnection`) keeps it on
   its pool entry, so the next transfer to the origin reads it back from `Session` and
   continues it - the next odd stream, the same HPACK tables, no second preface. The
   connection is marked reusable when the exchange left it intact and the peer has sent no
   GOAWAY and not closed it (`AcceptsNewStreams`); otherwise it is closed. A retry within
   the transfer (authentication, 417) goes on the next stream of the same connection on the
   same terms. Whoever closes the connection first calls `ShutDownAsync`, which sends curl's
   closing GOAWAY: the pool, for a connection not marked reusable and for an idle one it
   evicts, expires, finds dead or drops at the end of the run; the handler itself, on a
   connection that holds no session (`TryHoldSession` returns false, as every non-pooled
   connection does). The GOAWAY is last stream 0, NO_ERROR, debug data `shutdown` and a NUL
   (`00 00 11 07 00 00 00 00 00 00 00 00 00 00 00 00 00 73 68 75 74 64 6F 77 6E 00`,
   measured), and is not sent when no preface went out, a GOAWAY already has, or the peer
   closed the connection; a write that fails is ignored, as the connection is closing.
   Keeping the session keyed inside the HTTP library was rejected: it would need its own
   eviction, expiry and end-of-run disposal that the pool already has.
6. **Each stream's receive window grows to 10 MiB**, as curl 8.18.0 does: two identical
   WINDOW_UPDATEs on the stream right after the frame that ends the request - HEADERS for a
   request with no body, its last DATA otherwise - (measured, BL-817 Notes) and, for a body
   still waiting for window, before the first frame is read. The increment is 10 MiB less the
   initial window in force: 65535 (10420225) until the server acknowledges the client's
   SETTINGS, 65536 (10420224) after, as nghttp2 counts it. A stream an h2c upgrade continues
   sends none (not measured; left as it was).

## Consequences

- HTTP/2 transfers get every HTTP/1.1 behaviour already built, measured and tested, and
  HTTP/3 can take the same approach with a QUIC stream.
- Pooling, the stream window and the closing GOAWAY now match curl on the wire (BL-817,
  points 5 and 6). Known differences from curl still open: its `-v` output names streams
  (`[HTTP/2] [1] OPENED stream ...`), which is BL-660, and `-Z` multiplexes concurrent
  transfers over one connection, which is BL-717.

## Alternatives considered

- **A separate HTTP/2 exchange in the handler, reading header lists directly.** Rejected:
  every output, failure and retry rule would have to be written and measured twice, and
  would drift; curl itself does not work that way.
- **Deciding HTTP/2 inside the handler from `HttpRequestOptions` alone.** Rejected: ALPN
  is negotiated by the connector, so the connect result has to carry what was agreed.
