# ADR-0998: A running multi transfer with no socket the handle knows makes the timer 0

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4069, BL-3990, CGR-034). Replaces the
10 ms `RunningTransferCheckInterval` of ADR-0599 and ADR-0997.

## Context

Since ADR-0997 a running transfer whose socket the handle does not know (a fake or in-memory
connection, a non-`ManagedEasyTransfer`, a managed one before its dial has a socket) is told no socket.
`ManagedMultiTransfer` still kept the timer at most `RunningTransferCheckInterval` (10 ms) while such a
transfer ran, a number libcurl never gives. libcurl has no transfer without a socket; for a transfer
that can run on without waiting on one it calls `Curl_expire(data, 0)`, so the timer callback is told 0.
There is nothing to measure against real curl.

## Decision

- `RunningTransferCheckInterval` is gone. While a transfer runs and the socket callback is set, the
  timer is the earliest deadline of the transfers when every running one waits on its real socket
  (ADR-0973), else 0: a transfer on a socket wait still computing (ADR-0986) and a transfer with no
  socket the handle knows alike, as libcurl's `Curl_expire(data, 0)`.
- A timeout action wakes a transfer with no operating system socket, as before.

## Consequences

An application driving only socket actions is called back at once while such a transfer runs, and sees
it finish on its next timeout action. The timer callback sequences of the fake-transfer tests read
`timer 0` where they read `timer 10`.
