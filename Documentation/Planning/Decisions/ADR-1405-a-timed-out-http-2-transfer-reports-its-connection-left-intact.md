# ADR-1405: A timed-out HTTP/2 transfer reports its connection left intact

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-25
- Task: BL-5974

## Context

Curl wrote `closing connection #0` when `-m` ended an HTTP/2 transfer. Measured with curl
8.18.0 (nghttp2, WinGet), `Record-CurlExchange.ps1 -Http2Origin prior-knowledge`,
`--http2-prior-knowledge -v -m 2`, against a stream that stalls after a `200` head and DATA,
or after a `100` alone: the timeout line, then `Connection #0 to host 127.0.0.1:18274 left
intact`, exit 28. curl resets only the stream (`RST_STREAM` error 5), and a second URL goes
out on stream 3 of the same connection.

## Decision

An exchange that times out on a stream of an HTTP/2 connection, by prior knowledge, ALPN or an
h2c upgrade, reports the connection left intact on every platform. As for any multiplexed
connection (BL-717), the line waits for the last transfer on a shared one.

The connection is still not marked reusable: Curl closes it, resetting the stream before its
GOAWAY (BL-5906), so one URL sends the same frames as curl. Keeping it for the next URL needs
the session to survive a read cancelled mid-frame, which BL-5976 covers.

HTTP/3 is unchanged: its timeout has not been measured.

## Consequences

Until BL-5976, a second URL after a timeout opens a new connection although `-v` said the first
was left intact.
