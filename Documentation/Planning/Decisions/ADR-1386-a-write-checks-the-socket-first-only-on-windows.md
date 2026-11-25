# ADR-1386: A write checks the socket first only where aborts are reported as resets (BL-5939)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-0708 and ADR-0878.

## Context
On Linux, against a TELNET server that resets before any negotiation reply goes out, curl
8.18.0 prints `Sending data failed (104)` for the first reply and `(32)` for the rest. Curl
printed `(32)` for every reply (BL-5918, ADR-1382).

The cause is in `StreamConnection.WriteAsync`. Before each write it asked whether the socket
held unread bytes or the peer's FIN (ADR-0708, ADR-0878), on every platform, although only the
Windows aborted-to-reset rewrite (`ReportsAbortedReadAsReset`, ADR-0419) reads the answer. Once
a byte has been read, that check peeks one byte from a readable socket. On Linux the peek of a
reset connection with nothing left to read fails with the socket's pending `ECONNRESET` and
clears it, so the write that followed failed with `EPIPE`.

## Decision
`StreamConnection` runs the checks before a write only when `ReportsAbortedReadAsReset` is set,
which production sets on Windows alone. Windows behaves as before.

Measured on 2026-11-25 in WSL Ubuntu against `Record-CurlExchange.ps1 -ResetAfterResponse` on
the Windows host, with Curl published for `linux-x64`: Curl's `-v` lines after the connection
lines, and its exit 0, matched curl's, `(104)` then `(32)`. Which of curl's two outcomes a run
takes (this one, or exit 56 when the replies leave before the reset arrives) depends on timing.

## Consequences
- On Linux and macOS the first write after a reset reports the reset, in every protocol that
  runs over `StreamConnection`, as a plain `send` does.
- macOS is not measured; it takes the Linux answer.
