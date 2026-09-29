# ADR-0172 — HTTP/3 requests run on an `Http3Session`, and `--http3` falls back to TCP only when QUIC fails

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-731.

## Context

ADR-0144 decided that HTTP/3 is hand-built and that the HTTP handler
(`Curl.Protocol.Http.UnitLibrary`) chooses QUIC or TCP per transfer. ADR-0255 (BL-730)
built the HTTP/3 frames and QPACK in `Curl.Http3.UnitLibrary`. BL-731 puts them to work: the
handler must send a request on a QUIC stream and read its response through the same output,
`-i`/`-D`, `-f`, redirect, authentication, cookie and progress paths as HTTP/1.1 and HTTP/2.
A few choices were left open, and some failures have no measured text because no local HTTP/3
server can be made to fail on demand (ADR-0144); those are read from curl's
`lib/vquic/curl_ngtcp2.c` at `curl-8_18_0`.

## Decision

1. **One seam for HTTP/2 and HTTP/3.** `IHttpStreamSession` (a connection carrying a stream
   per request: `VersionName`, `UsingLine`, `AcceptsNewStreams`, `CreateStream`) and
   `IHttpStreamConnection` (one request stream as an `IConnection`, with `TrailerBytes`,
   `EndRequestAsync`, `ReadToEndAsync`) replace the handler's HTTP/2-only types.
   `Http2Session` and `Http2StreamConnection` implement them; `Http3Session` and
   `Http3StreamConnection` are new. The stream presents the response exactly as the HTTP/2
   one does, with `HTTP/3 200 \r\n` as the status line (ADR-0144 section 6), so every
   downstream path is shared. `%{http_version}` reports 3.0.
2. **The session is the transfer's `IConnection`.** A QUIC connect yields an
   `IMultiplexedConnection`; the handler wraps it in an `Http3Session`, which implements
   `IConnection` so the connect result, timings, `-v` connection lines and disposal follow the
   TCP path unchanged. Reading or writing the session itself throws `NotSupportedException`.
   Disposing it disposes every stream it opened, closes the connection with `H3_NO_ERROR`
   (`0x100`), ignoring an `IOException` from a connection already lost, and disposes it.
   It is never marked reusable, as HTTP/2's is not (BL-658).
3. **Client streams open with the first request.** Before the first request stream the
   session opens the control stream (type 0, curl's `SETTINGS`), then the QPACK encoder and
   decoder streams (types 2 and 3). QPACK runs with capacity 0 and 0 blocked streams both
   ways, as curl advertises, so requests use the static table and literals, with Huffman
   coding where shorter; the pinned GET field section is RFC 9204-correct byte for byte.
4. **The request field section** is `Http2RequestHeaders.Of`, since curl's HTTP/3 filter
   calls the same `Curl_http_req_to_h2`: `:method`, `:scheme`, `:authority`, `:path`, then
   the other headers lower-cased in order, as measured in ADR-0144. The request is framed as
   for HTTP/2 (`HttpRequestFraming.ForHttp2OrHttp3`): no chunking, no curl `Expect`, no wait
   for `100 Continue`. A request without a body ends the stream with its `HEADERS`; a body of
   known length ends it with its last `DATA` frame; one of unknown length ends it with an
   empty write carrying FIN.
5. **Connect selection** (ADR-0144 section 4): `--http3-only` with a URL that is not
   `https://` fails before connecting with exit 3 and `HTTP/3 requested for non-HTTPS URL`,
   reported with `closing connection #-1` (measured). `--http3-only` on `https://` connects
   over QUIC alone and its failure is the transfer's. `--http3` on `https://` tries QUIC and,
   when it fails, TCP; if TCP fails too the transfer fails with the QUIC attempt's exit code
   and message (measured), carrying TCP's timings and connection number. `--http3` on
   `http://`, and either option with a proxy configured, connect over TCP only. The race that
   also starts TCP once `--happy-eyeballs-timeout-ms` passes without a QUIC handshake is left
   to its own task, as is measuring what curl does with a proxy.

   *Amended by BL-835 (2026-09-29):* that race is now built, as ADR-0144 section 4 states it.
   `--http3` starts the TCP connect when the QUIC connect fails or once
   `HttpRequestOptions.HappyEyeballsTimeout` (default 200 ms) passes on the transfer's
   `TimeProvider` without it completing; the first to connect carries the transfer, and the
   other is cancelled and its connection disposed should it still complete. Both failing
   still reports the QUIC attempt's exit code and message. The title's "only when QUIC
   fails" describes BL-731's behaviour, not the current one.
6. **Failures** (from `curl_ngtcp2.c` where not measured):
   - The server resets the request stream: `HTTP/3 stream <id> reset by server`, exit 95
     `CURLE_HTTP3`, or exit 18 once body bytes have arrived (curl tests
     `data->req.bytecount`). BL-731's criterion said exit 95 for a reset mid-body; the source
     says 18, and the source wins. [ADR-0187](ADR-0187-http-3-stream-resets-follow-curl-8-21-0-and-a-refused-stream-is-retried-on-a-new-connection.md)
     moves this row and the `:status` row below to `curl-8_21_0`: a reset names its error
     code, one with `H3_REQUEST_REJECTED` is retried on a new connection, and `-I` ignores a
     reset after the head (BL-834).
   - The stream ends before the final head: `HTTP/3 stream <id> was closed cleanly, but before
     getting all response header fields, treated as error`, exit 95.
   - A head without a valid `:status`: the client aborts the stream with `H3_MESSAGE_ERROR`
     (`0x10e`), as nghttp3 does, and curl sees a stream closed with an error: `HTTP/3 stream
     <id> reset by server`, exit 95.
   - Frames or field sections that break RFC 9114 or RFC 9204 (`DATA` before the head, a
     control-stream frame on the request stream, a truncated frame, a reserved HTTP/2 type, a
     payload over the limit, a field section that refers to the dynamic table): exit 56
     `CURLE_RECV_ERROR` with `nghttp3_conn_read_stream returned error: <name>`, where the name
     is nghttp3's `ERR_H3_` plus the RFC's error name (`ERR_H3_FRAME_UNEXPECTED`,
     `ERR_H3_FRAME_ERROR`, `ERR_H3_EXCESSIVE_LOAD`), or `ERR_QPACK_DECOMPRESSION_FAILED`.
     curl fails the ingress with `CURLE_RECV_ERROR` and its first `failf` is nghttp3's.
   - A lost connection (`MultiplexedConnectionFailedException`, on open, write or read):
     the exit code and message the exception carries, which `Curl.Quic` chooses.
7. **Frame size** (amended by BL-838). `DATA` frames of any length stream, as nghttp3
   does: `Http3FrameReader.ReadFrameOrDataAsync` hands a `DATA` payload over piece by
   piece, and `Http3StreamConnection` reads it through a 16 KiB buffer
   (`DataBufferLength`). Every other frame on a request stream - `HEADERS` among them -
   is still read whole, so only a non-`DATA` frame over 16 MiB
   (`MaximumFramePayloadLength`) fails, with exit 56 and `ERR_H3_EXCESSIVE_LOAD`.
8. **The server's streams** (BL-836, decided by Claude under Stewart's delegation). The
   session accepts the server's unidirectional streams from its creation to its disposal,
   one background reader per stream: the control stream through
   `Http3ControlStreamReader`, the QPACK encoder and decoder streams into the session's
   `QpackDecoder` and `QpackEncoder`, and a stream of an unknown or grease type abandoned
   with `H3_STREAM_CREATION_ERROR` (RFC 9114 section 6.2). A `GOAWAY` makes
   `AcceptsNewStreams` false, so the handler takes no further request on the connection;
   it fails nothing in flight. The first `Http3Exception` or `QpackException` on any of
   them is fatal to the connection, as `cf_ngtcp2_h3_err_is_fatal` makes an
   `nghttp3_conn_read_stream` error at curl tag `curl-8_18_0`: the session closes the
   connection with that error's code and fails the transfer's next read - or the read the
   close interrupts - with exit 56 and `nghttp3_conn_read_stream returned error: <name>`,
   where a QPACK stream error is `ERR_QPACK_ENCODER_STREAM_ERROR` or
   `ERR_QPACK_DECODER_STREAM_ERROR`. Later errors change nothing. A reset of a stream whose
   type has arrived is `ERR_H3_CLOSED_CRITICAL_STREAM`, since only critical streams are
   still read by then; a reset before the type is ignored, as the stream's purpose is
   unknown. A lost connection, or one that hands out no server streams
   (`NotSupportedException`), ends the reading quietly: the request stream reports a lost
   connection itself.

## Consequences

- An HTTP/3 transfer writes the same output and takes the same `-f`, redirect, retry,
  cookie and progress paths as HTTP/2, tested over `FakeMultiplexedConnection` with no QUIC.
- The server's control and QPACK streams are read (section 8), so a `GOAWAY` stops further
  requests and a broken control or QPACK stream fails the transfer as curl does.
- Some failure texts come from curl's source rather than a run; the task that measures
  against a failing HTTP/3 server replaces them if they differ.

## Alternatives considered

- **Keep the handler's HTTP/2 types and add parallel HTTP/3 ones.** Two copies of every
  branch in the exchange for one concept, a stream per request. Rejected for the two
  interfaces.
- **A separate `IConnection` adapter for the QUIC connection.** It would hold the same
  connection as the session and nothing else. Rejected; the session implements it.
- **Map protocol errors to exit 95.** Reads naturally from "HTTP/3 error", but curl's
  ingress path returns `CURLE_RECV_ERROR` for them; rejected in favour of the source.
