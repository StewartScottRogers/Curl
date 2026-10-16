# ADR-0597 — The managed engine's multi handle: timeout, wait, poll and wakeup

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2425.

## Context

ADR-0594 gave `ManagedMultiTransfer` add, remove, perform and info_read, and left
`curl_multi_timeout`, `curl_multi_wait`, `curl_multi_poll` and `curl_multi_wakeup` throwing
`NotSupportedException`. The managed engine runs each transfer as one asynchronous
`IEasyTransfer.PerformAsync` (ADR-0594), so it owns no operating system poll set of its own and keeps
no libcurl-style per-transfer timer list. The waits must run on the engine's injected `TimeProvider`
so they can be tested without real time.

## Decision

1. **Timeout.** `GetTimeout` answers 0 while a perform has work: a transfer added and not started,
   or one whose perform completed and is not yet reported. Otherwise it answers the milliseconds,
   rounded up and never below 0, until the first running transfer's `CURLOPT_TIMEOUT`
   (`EasyTransferOptions.Timeout`) expires, measured on the engine's `TimeProvider` from the multi
   perform that started it; -1 when no running transfer has one. The managed transfer enforces its own
   timers, so this is the deadline the application may wait to, not a timer the multi must act on.
2. **Wait.** `WaitAsync` returns when a running transfer's perform completes (counted as one ready
   socket, as its socket would be in libcurl), when an extra socket has one of its events (each
   extra socket's `ReturnedEvents` is set on every check), at the timeout, or sooner at the handle's
   own timeout, as libcurl lowers `timeout_ms` to `curl_multi_timeout`. With no running transfer and
   no extra socket it returns at once, as libcurl's `curl_multi_wait` does.
3. **Poll.** `PollAsync` is the same, but with nothing to wait on it waits the whole timeout, and a
   `Wakeup` ends it with `Ok` and 0.
4. **Wakeup.** `Wakeup` may be called from any thread. Like libcurl's wakeup socket pair it is
   sticky: with no poll running, the next poll returns at once, and any number of wakeups before it
   end only that one.
5. **Extra sockets.** The engine has no poll set to add an application's descriptors to, so a wait
   with extra sockets asks an internal `IWaitSocketPoller` about them each `SocketPollInterval`
   (10 milliseconds) while it waits. The production `SocketWaitPoller` wraps the descriptor in a
   `Socket` that does not own it and calls `Socket.Poll` with no wait: `POLLIN` as `SelectRead`,
   `POLLOUT` as `SelectWrite`, `POLLPRI` as `SelectError`, which reports urgent data. The 10 ms
   interval keeps a ready descriptor's latency well under a frame without spinning.
6. **Arguments.** A negative timeout answers `BadFunctionArgument` (libcurl's check on a negative
   `timeout_ms`); every member answers `BadHandle` once the handle is disposed, `GetTimeout` with -1.
   A cancelled token ends a wait with `OperationCanceledException`.

## Consequences

`Libcurl`'s `curl_multi_timeout`, `_wait`, `_poll` and `_wakeup` can drive the managed engine, and an
application's loop of perform, wait and info_read no longer has to poll `PerformAsync`. A transfer's
connect timeout and the other libcurl timers are not reported by `GetTimeout`: the managed transfer
enforces them itself, and only the overall timeout is a deadline an application can plan by.
