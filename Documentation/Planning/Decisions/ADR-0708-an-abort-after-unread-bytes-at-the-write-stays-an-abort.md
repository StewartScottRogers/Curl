# ADR-0708 — An aborted read stays an abort when the peer's bytes were unread at the write

- Status: Accepted
- Date: 2026-10-19
- Task: BL-2821 (GF-0096)
- Amends: ADR-0419
- Decided by Claude under Stewart's delegation.

## Context

ADR-0419 rewrites every Windows read failing with `SocketError.ConnectionAborted` as
`SocketError.ConnectionReset`, measured for a peer that resets right after accepting. Upstream
test1802 has another shape: the proxy answers the CONNECT with 200, `Connection: close` and six
leftover bytes, then closes. `HttpProxyTunnel` reads the reply one byte at a time, so those six
bytes stay unread on the socket; Curl sends the GET into the closed tunnel, the peer answers that
write with an RST, and the next receive fails. curl 8.22.0's Schannel build prints
`curl: (56) Recv failure: Connection was aborted` in 9 of 11 runs (GF-0096); Curl printed
`Connection was reset` in 6 of 6, because of the rewrite.

Measured on 2026-10-19 with `Record-CurlExchange.ps1`: a zero-time `Poll(SelectRead)` before the
write is true in both shapes (the `-Reset` RST has often arrived by then), and a one-byte peek
returns 0 bytes for that early RST as for a FIN, so neither tells them apart. `Socket.Available`
does: six in test1802, zero after the early RST.

## Decision

`StreamConnection.WriteAsync` records, before each write, whether the socket under a
`NetworkStream` had unread bytes (`HasUnreadBytesBeforeWrite`, `Socket.Available > 0`). The
aborted-to-reset rewrite applies only when the last write found none, the ADR-0419 shape. When
bytes were unread, the abort is kept and `CurlSocketErrorText.ReceiveFailure` prints curl's
`Connection was aborted`.

## Consequences

- test1802's closed CONNECT tunnel prints curl's `Recv failure: Connection was aborted` (4 of 4).
- ADR-0419's `-Reset` URL still prints `Recv failure: Connection was reset` (4 of 4).
- A peer that closed with no bytes left unread keeps ADR-0419's reset wording; that shape was not
  measured against curl.
- Streams other than `NetworkStream` never have unread bytes by this check, so they keep ADR-0419's
  rule.
