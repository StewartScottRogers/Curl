# ADR-1407: A timed-out HTTP/2 transfer's connection carries the next transfer

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-25
- Task: BL-5976
- Replaces ADR-1405's "the connection is still not marked reusable". The rest of ADR-1405 stands.

## Context

Curl wrote `Connection #0 to host ... left intact` after `-m` ended an HTTP/2 transfer, then
dialled a new connection for the next URL. Measured with curl 8.18.0 (nghttp2, WinGet),
`Record-CurlExchange.ps1 -Http2Origin prior-knowledge`,
`--http2-prior-knowledge -v --trace-config http/2 -m 2 <url>/x <url>/y` against a stream that
stalls after a `200` head and DATA: `[HTTP/2] [1] -> FRAME[RST_STREAM, len=4, flags=0, error=5]`,
`left intact`, `Reusing existing http: connection`, HEADERS on stream 3, the same reset and line
for stream 3, and one GOAWAY. Exit 28.

A time limit cancels the wait for the server's bytes. The frame codec had consumed part of the
frame it was reading by then, so the connection could not be read again.

## Decision

- A frame is read only once it is buffered whole (`ReadAheadConnectionStream`), so a cancelled
  wait leaves every byte for the next transfer. The buffer holds one frame of the largest size
  the client accepts; a longer frame is read from its header alone, to be refused as before.
- A timed-out transfer resets its stream with STREAM_CLOSED, unless the stream has ended both
  ways, and marks the connection reusable, by prior knowledge, ALPN or an h2c upgrade, on every
  platform.
- A connection on which a write was cancelled may hold part of a frame on the wire. It takes no
  new stream, so it is closed, as is one that fails the reset. curl instead keeps the unsent
  bytes and sends them later; Curl's `IConnection` cannot say how much of a cancelled write
  went out, and a closed connection is safe where a corrupted one is not.

HTTP/3 is unchanged: its timeout has not been measured.

## Consequences

The frames Curl sends for two timed-out URLs are those curl sends. A time limit that cuts a
blocked upload write still dials a new connection for the next URL.
