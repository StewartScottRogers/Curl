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

## Amendment (BL-2203, 2026-10-13): curl 8.22.0 asks only after a second idle

Decided by Claude under Stewart's delegation. The gap office's reference, curl 8.22.0
(`Git\ucrt64\bin\curl.exe`), no longer asks a connection used under a second ago. Measured with
`Record-CurlExchange.ps1 -Connections 2`, the server closing after each `200` with
`Content-Length: 26`, `-v --rate R URL URL`:

- `--rate 66/m` (0.9 s apart), 2 of 2, and `120/m` (0.5 s): `Reusing existing http: connection`,
  `Connection died, retrying a fresh connect (retry count: 1)`, `shutting down connection #0`,
  then connection #1.
- `--rate 54/m` (1.1 s apart), 2 of 2, and `30/m` (2 s): no `Reusing`, no `seems to be dead` and
  no `shutting down` line; `Hostname ... was found in DNS cache` and connection #1.
- `--digest` with `-HalfCloseAfterResponse` (this ADR's case), 2 of 2: `left intact`,
  `Issue another request`, `Reusing existing http: connection`, `Connection died`, a fresh
  connect. The retry goes through the pool, so the handler's rule (decision 2) already gives
  these lines once the pool hands the connection out.

So decision 3 becomes: `PoolingConnector` asks `HasPeerClosed` only of a connection idle for
`PoolingConnector.LivenessCheckIdleTime` (one second) or longer, and closes one that answers `true`
without a line, as curl 8.22.0's once-a-second prune does; one idle for less is handed out without
asking, and the HTTP handler's `Connection died` resend (ADR-0489) takes over when it proves
closed. A connection a read already found closed (ADR-0112) is still reported dead with both lines.
The second is measured from when the connection was returned to the pool; curl times its prune
from the last prune, which the measurements cannot tell apart. Upstream test675 depends on this.
