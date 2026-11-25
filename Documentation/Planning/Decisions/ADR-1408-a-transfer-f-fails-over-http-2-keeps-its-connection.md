# ADR-1408: A transfer -f fails over HTTP/2 keeps its connection

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-25
- Task: BL-5977

## Context

Curl wrote `closing connection #0` after `-f` failed an HTTP/2 transfer, and dialled a new
connection for the next URL. Measured with curl 8.18.0 (nghttp2, WinGet),
`Record-CurlExchange.ps1 -Http2Origin prior-knowledge`,
`--http2-prior-knowledge -sS -v -f -m 5 <url>/a <url>/b` against a stream answered `500`:

| Stream 1 after the `500` head | Frame curl sends for stream 1 |
|-------------------------------|-------------------------------|
| the head ends it              | none                          |
| left open                     | `RST_STREAM` 5 (STREAM_CLOSED) |
| DATA ending it arrives too    | `RST_STREAM` error -902       |

In all three curl writes `Connection #0 to host ... left intact`, then
`Reusing existing http: connection with host ...`, sends HEADERS on stream 3 of the same
connection, and sends one GOAWAY when it is done. Exit 22 for the first URL. BL-5975 measured
the same line for a `500` that ends the stream during the wait for `100 Continue`.

## Decision

A transfer `-f` fails on an HTTP/2 stream ends only its stream, as one `-m` ends does
(ADR-1407): the connection is reported left intact and marked reusable while its session takes
new streams, and the stream is reset with STREAM_CLOSED at once when it has not ended both
ways. HTTP/1.x and HTTP/3 are unchanged.

Where DATA that ends the stream has arrived but is unread, Curl sends `RST_STREAM` 5. curl's
`-902` is nghttp2's `NGHTTP2_ERR_CALLBACK_FAILURE` written into the frame's error code, which
no HTTP/2 error code names and no output or exit code of the tool shows.

## Consequences

The next URL to the same origin goes out on the same connection. The unread frames of a reset
stream are dropped when a later transfer reads past them.
