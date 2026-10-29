# ADR-0983: A wrapped transfer runs on its wrapped engine transfer's socket wait, and a computing one asks for timer 0

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4038, for CGR-034).

## Context

`ManagedMultiTransfer.Start` gave a `TransferSocketWait` only to an added handle that is itself a
`ManagedEasyTransfer`. `Curl.Api`'s `StoreBoundEasyTransfer` wraps one, so on a socket-action multi handle it was
told a synthetic socket from `FirstTransferSocket` and brought back by the 10 ms `RunningTransferCheckInterval`
timer, never by its real socket. A transfer on a socket wait that still waits on nothing when
`SocketWaitSettleLimit` ends (it is computing) relied on the same 10 ms timer.

## Decision

- `IEasyTransfer` gets `PerformingTransfer`, the handle whose perform does the transfer: the handle itself by
  default, the wrapped handle's for a wrapper. `StoreBoundEasyTransfer` forwards it. The multi handle gives the
  `PerformingTransfer` the socket wait and multi callbacks when it is a `ManagedEasyTransfer`, so the wrapper is
  told the real socket the wrapped transfer waits on. `TransferSocketWait` is internal to the engine, so the
  seam names the handle, not the wait.
- A fake that wraps no engine transfer keeps the synthetic `FirstTransferSocket` number and the 10 ms timer: it
  has no socket to tell.
- While any running transfer on a socket wait waits on nothing (`Waiting` is `None`) after the settle, the timer
  callback is told 0, as libcurl's `Curl_expire(data, 0)` for a transfer that can run on; the next timeout
  action settles it again.

## Consequences

- A `CurlClient` with stores drives its transfers by their real sockets on a socket-action handle.
- A computing transfer is brought back at once rather than after 10 ms; each action still waits at most
  `SocketWaitSettleLimit`, so the loop does not spin.
