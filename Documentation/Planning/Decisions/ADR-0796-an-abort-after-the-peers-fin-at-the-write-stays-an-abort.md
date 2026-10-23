# ADR-0796 — An aborted read stays an abort when the peer's FIN was unread at the write

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3304
- Amends: ADR-0419, ADR-0708
- Decided by Claude under Stewart's delegation.

## Context

ADR-0419 rewrites a Windows read failing with `SocketError.ConnectionAborted` as
`SocketError.ConnectionReset`; ADR-0708 keeps the abort when the last write found unread bytes.
Reusing a kept connection the server had closed (FIN) still printed `Recv failure: Connection was
reset` where curl 8.22.0 Schannel prints `Recv failure: Connection was aborted` before
`Connection died, retrying a fresh connect` (Record-CurlExchange.ps1 -Connections 3, a 401 with
Negotiate, NTLM, Digest and Basic challenges and Content-Length 1, `-sv --anyauth -u u:p`).

Measured with a C# file-based app over loopback: after the peer's FIN, a write the peer answers
with RST makes every following receive fail with WSAECONNABORTED (10053), synchronous,
`Socket.ReceiveAsync` and `NetworkStream.ReadAsync` alike, polled first or not. Windows aborts a
connection in CLOSE_WAIT that is reset, so curl's prompt `recv` sees the abort too; ADR-0419's
timing window applies only to a reset with no FIN before it.

## Decision

`StreamConnection` checks before each write whether the peer had sent unread bytes or its FIN
(`HasUnreadBytesOrEndBeforeWrite`): the socket is readable and a one-byte peek returns zero. A
peek that fails (a reset already there) is not an end, so ADR-0419's rewrite still applies. When
either held, an aborted read stays an abort. Linux and macOS are unchanged (no rewrite there).

## Consequences

The reused-connection retry prints `Recv failure: Connection was aborted` as curl's Schannel build
does. The peek is in the coverage-excluded socket adapter (ADR-0083); the decision it feeds is
pinned by `StreamConnectionTests`.