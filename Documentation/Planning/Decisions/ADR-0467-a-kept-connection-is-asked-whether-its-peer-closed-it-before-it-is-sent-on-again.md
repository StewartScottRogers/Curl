# ADR-0467: A kept connection is asked whether its peer closed it before it is sent on again

- Status: Accepted
- Date: 2026-10-10
- Task: BL-2018 (found by BL-2000, GF-0004)
- Decided by Claude under Stewart's delegation.

## Context

curl 8.21.0 asks a kept connection whether it is alive before it reuses it
(`Curl_conn_is_alive`: the socket is readable and a peek reads nothing). Measured on Windows
(Schannel, 2026-10-10, `Record-CurlExchange.ps1 -HalfCloseAfterResponse`): a server that answers
`--digest`'s first request with a `401` and then shuts down its send side makes curl write
`Connection 0 seems to be dead` and `shutting down connection #0`, and send the authenticated
request on connection #1, with nothing more written to #0. curl's check races the FIN: in one of
three runs it still reused #0 and wrote `Connection died, retrying a fresh connect`.

Curl had no such check. It knew a connection was dead only when an earlier read had returned
zero (ADR-0112), so the HTTP handler sent the retry on the closed connection every time, and only
then found it dead.

## Decision

1. `IConnection.HasPeerClosed` tells, without blocking and without taking a byte, whether the
   peer has closed the connection. The default is `false`. `StreamConnection` answers it over a
   `NetworkStream` with `Socket.Poll(0, SelectRead)` and `Available == 0`, inside an
   `[ExcludeFromCodeCoverage]` socket adapter (ADR-0083); `SslStreamConnection`,
   `HandBuiltTlsConnection` and `TcpIoTraceConnection` ask the connection they run over, and
   `PooledConnection` also answers `true` once a read found the close (ADR-0112).
2. `HttpProtocolHandler` sends a retry on the same connection only while `HasPeerClosed` is
   `false`. Otherwise the connection is left intact and the retry is issued as a new request, so
   the pool takes the connection back.
3. `PoolingConnector` reports an idle connection whose `HasPeerClosed` is `true` dead, as it
   does one a read found closed: `Connection N seems to be dead`, `shutting down connection #N`,
   and a fresh connect.

## Consequences

A half-closed connection now gives curl's usual answer, and its request bytes on #0 match.
A connection whose FIN arrives after the check still takes the `Connection died` retry, as in
curl. A TLS connection whose peer has sent `close_notify` but not yet a FIN is readable with
bytes waiting, so it still counts as open, as in curl's peek.
