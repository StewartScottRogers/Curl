# ADR-1432: A response body throws only to its reader

- Status: Accepted
- Date: 2026-11-26
- Task: BL-6048

Decided by Claude under Stewart's delegation.

## Context

A Surly bug report shows a program on macOS ending with exit 134 when a timed
`CancellationToken` cancels a streaming `CurlResponse.Body` read. `ResponseBodyStream` joined the
caller's and the client's tokens with a linked `CancellationTokenSource`, so anything a callback on
the transfer's token threw went back to whoever cancelled: for `CancelAfter`, a timer's thread, where
an unhandled exception ends the process. The task running the transfer could also fault while nobody
awaited it, and the buffer then never ended, so a pending read waited for ever.

The macOS abort was not reproduced: development runs on Windows, and no callback of the managed
engine is known to throw. This decision closes the ways `Curl.Api`'s body pipeline could cause it.

## Decision

- `ResponseBodyStream` forwards the caller's and the client's tokens to its own token source through a
  callback that catches the `AggregateException` of `Cancel()`. The reader still gets
  `OperationCanceledException`; the other callbacks on the transfer's token still run.
- The transfer's task and the closing task never fault. What running or disposing the transfer throws
  ends `ResponseBodyBuffer` (`Fail`), which throws it from `StartAsync` or `ReadAsync` after the bytes
  already delivered (ADR-1334), or `OperationCanceledException` when the transfer had been cancelled
  (ADR-0639). Disposing the body no longer rethrows it.

## Consequences

An exception from a token callback is not reported anywhere: there is no thread it can safely be
thrown on, and the read reports the cancellation that caused it.
