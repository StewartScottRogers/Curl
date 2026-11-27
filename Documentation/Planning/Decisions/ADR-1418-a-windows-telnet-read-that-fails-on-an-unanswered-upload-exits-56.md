# ADR-1418: A Windows TELNET read that fails on an unanswered upload exits 56 (BL-6012)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1352.

## Context
Fitness finding STD-201 (review of 2026-11-26): the server sends its negotiation, then
subnegotiation requests and `login: `, reads until it sees `quit` in the upload, sends a last
line and closes with client data unread, so the close is a reset. When the reset destroyed the
last line, Curl on Windows wrote `login: ` only and exited 0.

Measured on 2026-11-26 on Windows against a loopback server with that script
(`-t TTYPE=vt100 -t XDISPLOC=host:0 -t NEW_ENV=USER,bob -T file`):

| curl | Server sends the last line, then resets | Server resets 300 ms after `quit`, sending nothing |
|---|---|---|
| 8.21.0 (Schannel), 30 and 20 runs | `0/21` x7, `55/0` x17, `56/7` x6 | `0/7` x20 |
| 8.22.0 (Schannel), 25 and 25 runs | `0/21` x3, `55/0` x12, `56/7` x10 | `0/7` x25 |

curl reads its socket events: a reset that arrives with data pending fails the `recv` (56),
one that arrives alone is the close event (0). The reset discards the pending data, so a .NET
read fails the same way in both columns and Curl has only the session's state to judge by.

## Decision
On Windows a failed read is also a receive failure (exit 56) when upload bytes have been sent
in the session, and a reply or upload bytes have gone out since data was last written to the
output. The server was then being talked to, and what it had to say may be lost.
ADR-1352's two cases stand. The rule is `TelnetOutbox.FailedReadIsReceiveError`.

So:
- data written after the client's last send, then a reset: exit 0, as curl's `0/21`;
- a session with no upload bytes sent, data written, then a reset: exit 0, as before;
- an upload the server never answered, then a reset: exit 56.

## Consequences
- STD-201's loop no longer reports exit 0 with the last line lost.
- In the table's second column curl exits 0 and Curl exits 56. Nothing a .NET read reports
  separates it from the first column, and reporting success for truncated output is the worse
  error of the two.
- A server that sends the data which follows the upload in more than one piece, and resets after
  Curl has written one of them, still ends in exit 0.
- No timer is used, for ADR-1352's reason. Linux and macOS keep ADR-1382.
