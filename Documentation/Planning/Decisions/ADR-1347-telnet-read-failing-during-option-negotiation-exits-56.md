# ADR-1347: A TELNET read that fails during the option negotiation exits 56 (BL-5882)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context
The handler took every failed connection read as the server closing, with exit 0 (BL-077,
measured against curl 8.21.0 on Windows). Fitness finding STD-201: a server that closes with
the client's data unread resets the connection while options are being negotiated, and the
session then ended with exit 0 and the server's data lost.

Measured against curl 8.23.0 (Windows, Schannel) on 2026-11-24 with a loopback server:

- Negotiation, then data and a close that resets: `0/18`, `56/7`
  (`Recv failure: Connection was reset`) or `55/0`, depending on timing.
- A reset 300 ms after `login: ` was read, with or without one `DO TERM-TYPE` before it: exit 0.

curl on Windows tells the two apart by its socket events: a reset of an idle session arrives
as the close event alone, while one that strikes with data pending fails the `recv`. A .NET
read fails the same way for both, so the handler judges by the session's state instead.

## Decision
A failed read ends the session with exit 56 when either

- a negotiation reply could not be sent, or
- negotiation replies were sent and no data has been written to the output yet.

The message is `Recv failure: <socket text>`, or `Failure when receiving data from the peer`
when the failure holds no socket error. Any other failed read still ends it with exit 0.
The rule is `TelnetOutbox.ReadFailureLosesData`.

No timer is used: a rule that depends on how soon the reset follows would make the exit code
vary with machine load.

## Consequences
- STD-201's loop no longer reports exit 0 with nothing downloaded.
- A server that only negotiates, stays silent and then resets gets exit 56, where curl on
  Windows, seeing only the close event, would be expected to exit 0 (not measured).
- When every reply was sent and some data was written before the reset, the exit stays 0,
  where curl can report 56 if more data was pending.
