# ADR-1410: A timed-out HTTP/3 transfer cancels its stream and keeps its connection

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-25
- Task: BL-5978
- Replaces ADR-1407's "HTTP/3 is unchanged: its timeout has not been measured." and
  ADR-0164's omission of Stream Cancellation at table capacity 0.

## Context

Curl wrote `closing connection #0` when `-m` ended an HTTP/3 transfer, sent nothing for the
stream and dialled a new connection for the next URL. Measured with curl 8.18.0 (ngtcp2
1.21.0, nghttp3 1.15.0, LibreSSL, WinGet) against a System.Net.Quic server that answers
`200` and six bytes of DATA and never ends the stream,
`--http3-only -k -v --trace-config http/3 -m 2 <url>/x <url>/y`, exit 28:

- `-v`: `Operation timed out after 2015 milliseconds with 6 bytes received`,
  `[HTTP/3] [0] easy handle is done`, `[HTTP/3] no active streams, unset keep-alive`,
  `[HTTP/3] query conn[0]: MAX_CONCURRENT -> 99 (0 in use)`,
  `Connection #0 to host 127.0.0.1:18443 left intact`, then
  `Reusing existing https: connection with host 127.0.0.1` and the second request on stream 4,
  ending the same way (`-> 98`).
- Wire: STOP_SENDING with `H3_REQUEST_CANCELLED` (0x10c) on stream 0, and `40` on the
  client's QPACK decoder stream (Stream Cancellation, stream 0), although curl advertises a
  dynamic table of capacity zero. For stream 4, 0x10c and `44`.

## Decision

- A timed-out HTTP/3 transfer aborts its stream with `H3_REQUEST_CANCELLED` unless the stream
  has ended both ways (RESET_STREAM too while the request is unfinished), writes the Stream
  Cancellation, marks the transfer done and reports the connection left intact. The next URL
  reuses it, on every platform.
- `QpackDecoder.CancelStream` queues the instruction at capacity zero too. RFC 9204 section
  4.4.2 allows leaving it out; nghttp3 sends it.
- A connection that fails the write takes no new stream.
- `-f` over HTTP/3 is unchanged until measured (BL-5983).

## Consequences

Run against the same server, Curl sends the same abort code and decoder stream bytes and
writes the same lines. One trace line is still missing: during the next transfer curl writes
`[HTTP/3] [0] quic close(app_error=268) -> 0` for the cancelled stream, once QUIC closes it
(BL-5982).
