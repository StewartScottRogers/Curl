# ADR-0901: A multi-driven transfer's callbacks run inside perform, on its caller's thread

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3562 (CGR-033)

Decided by Claude under Stewart's delegation.

## Context

The managed engine runs each transfer a multi handle starts as its own `IEasyTransfer.PerformAsync`, a task
that goes on by itself (ADR-0594). Its write, header, read, debug, progress and other callbacks therefore ran
on thread-pool threads at any moment, while the application's own code ran. libcurl calls every callback of
a multi-driven transfer from inside `curl_multi_perform` (or `curl_multi_socket_action`) on the caller's
thread, and C code written for that contract takes no locks. Measured with libcurl 8.22.0 (Schannel) and a
loop of `curl_multi_perform` then `Sleep(30)`: `header:caller-thread:inside-perform
write:caller-thread:inside-perform`; Curl gave `other-thread:OUTSIDE-perform` for both.

## Decision

The transfers keep running as tasks, but their callbacks are marshalled:

- `ManagedMultiTransfer` owns a `MultiCallbackQueue`. Before each perform it starts it gives a
  `ManagedEasyTransfer` a `TransferCallbackMarshal`, which the perform reads once and wraps every
  `EasyCallbackOptions` delegate with.
- A wrapped callback called on the thread inside the multi handle's perform (the transfer's synchronous start,
  or a callback the perform is running) runs at once. Called from any other thread, it is queued and that thread
  waits; each `PerformAsync` and `SocketActionAsync` runs the queued callbacks first, as code inside the
  handle's transfer (`IsInTransferCallback`).
- After running a callback the perform waits up to `MultiCallbackQueue.FollowUpWait` (20 ms) for the next
  one, so a transfer is handed every chunk already received in one call, as libcurl's perform is; without it a
  loop that waits 500 ms between performs (upstream test2407) did not finish.
- A queued callback counts as work: `GetTimeout` answers 0, a wait or poll counts one ready socket and ends
  when one is queued.
- Once the transfer is removed or the handle cleaned up (its token cancelled), or its perform has ended, a
  callback runs on the thread that calls it, so teardown never waits for a perform that may not come.

Span arguments are copied for the hand-over, and a read callback fills a buffer of its own copied back.

## Consequences

Callbacks of a multi-driven transfer never run at the same time as the application. A transfer moves on only
while the application performs, as libcurl's does. The curl tool's parallel transfers set no
`EasyCallbackOptions`, so they are unchanged.
