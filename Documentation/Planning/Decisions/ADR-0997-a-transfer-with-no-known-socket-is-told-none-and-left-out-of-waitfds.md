# ADR-0997: A multi transfer with no socket the handle knows is told no socket and is left out of GetWaitSockets

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4068, BL-4072, BL-4073, CGR-034). Its timer point is replaced by ADR-0998: the timer is now 0 while such a transfer runs.

## Context

`ManagedMultiTransfer` used to number a running transfer whose connection is not an operating
system descriptor from `FirstTransferSocket` (`0x40000000`), told that number to
`CURLMOPT_SOCKETFUNCTION` and listed it in `GetWaitSockets`. An application that took the number
for a descriptor and passed it to `select`, `poll` or `epoll` waited on something that does not exist.
Real libcurl has no such transfer: every transfer it runs has a real socket or none yet, so there is
nothing to measure against.

## Decision

- A running transfer whose socket the handle does not know (a fake or in-memory connection, an
  HTTP/3 or other engine-owned transport) is told no socket: `MultiOptions.Socket` is not called for it
  and `GetWaitSockets` leaves it out. `FirstTransferSocket` and the synthetic numbers are gone (BL-4072).
- A transfer with a real socket (a `TransferSocketWait`, ADR-0973, ADR-0982) is unchanged.
- While any transfer runs the timer still asks to be called back every `RunningTransferCheckInterval`
  (10 ms, ADR-0599), which is what brings the application back to see such a transfer finish.
- `ResolverWakeupSocket` stays as ADR-0604 decided.

## Consequences

An application driving only socket actions sees no socket for such a transfer and relies on the timer,
which is what libcurl does for a transfer that is resolving or connecting without a socket yet.
