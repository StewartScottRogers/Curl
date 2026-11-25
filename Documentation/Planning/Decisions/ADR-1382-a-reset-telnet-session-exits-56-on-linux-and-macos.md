# ADR-1382: A reset TELNET session exits 56 on Linux and macOS (BL-5918)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1352.

## Context
ADR-1352 pinned curl on Windows: a reset TELNET session exits 0 unless the reset strikes the
option negotiation (then 56). The handler applied that on every platform.

Measured against curl 8.18.0 (Ubuntu, OpenSSL, in WSL) on 2026-11-25, with a server on the
Windows host that resets the connection (`Record-CurlExchange.ps1 -Reset` and
`-ResetAfterResponse`):

| Server | curl on Linux |
|---|---|
| Resets on accept, or after 500 ms of silence with nothing sent | 56 |
| Sends `login: `, resets (at once or 500 ms later) | 56, `login: ` written |
| Sends `IAC DO TERM-TYPE`, resets 500 ms after the replies | 56 |
| Sends `IAC DO TERM-TYPE` and `login: `, resets 500 ms after the replies | 56, `login: ` written |
| Sends a negotiation (with or without data) and resets before any reply goes out | 0, `Sending data failed (104)` then `(32)` under `-v` |

Every 56 prints `curl: (56) Recv failure: Connection reset by peer`. In the last row the first
reply's send takes the socket's pending error, so the next read finds an ordinary close.
curl's `master` `lib/telnet.c` keeps the receive error for `ECONNRESET`; it only adds a debug
log, which its comment ties to macOS.

The recorder's reset options sent a FIN before the RST (`TcpClient.Close` shuts the socket
down first), so Linux saw an ordinary close. They now close the socket itself, sending the
RST alone.

## Decision
On Linux and macOS every failed connection read ends the session with exit 56, with
`Recv failure: <socket text>`, or `Failure when receiving data from the peer` when the failure
holds no socket error. Windows keeps ADR-1352's rule. The rule is
`TelnetOutbox.FailedReadIsReceiveError`, told the platform by the handler's internal
`RunsOnWindows`, which tests set to run either rule anywhere.

The last row needs no rule: a read after a failed send returns no bytes, which already ends
the session with exit 0. Curl published for `linux-x64` matched every exit code and message
above against the same server.

## Consequences
- macOS is not measured; it takes the Linux answer, as curl's source treats the two alike.
- Under `-v`, Curl on Linux reported `(32)` for the first reply of the last row where curl
  reports `(104)`; that is filed as its own task.
