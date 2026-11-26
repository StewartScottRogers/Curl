---
id: BL-6006
title: Bound the failed-stream cleanup that no longer honours the transfer's token
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6006 — Bound the failed-stream cleanup that no longer honours the transfer's token

## Goal

A failed HTTP/2 or HTTP/3 transfer's stream cleanup ends within a bounded time when the peer stalls, and the transfer still exits with its original failure code.

## Context

Found by the shift-end review of 2026-11-26 (ADR-0710), silent-failure-hunter and code-reviewer agents, in commit 6e8d74c665 (BL-5998).

`Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs:132-142`: `ResetFailedStreamAsync` now passes `CancellationToken.None` to `EndGivenUpStreamAsync` (HTTP/3 `CancelUnlessEndedBothWaysAsync`, HTTP/2 `ResetUnlessEndedBothWaysAsync`) and to `ResetFailedStreamUploadAsync` (HTTP/2 `ResetUnendedRequestOfEndedResponseAsync`). That fixed BL-5998: a token firing during cleanup no longer replaces exit 22 with a cancellation. But on HTTP/2 these write a RST_STREAM frame on the shared connection. If the peer has stopped reading (full TCP send buffer, or the session's write lock held by a stream blocked on flow control), that write now waits with nothing to stop it: not Ctrl+C, not `--max-time`, not `-Y`/`-y`. The transfer has already failed, but the process hangs instead of exiting. The timeout path (`HttpProtocolHandler.ResponseExchange.cs:115`) already did this; BL-5998 spread it to every `HttpTransferException` path. The test BL-5998 added covers only HTTP/3, where the abort is synchronous.

Fix: run the cleanup under its own short bounded token, made from the injected `TimeProvider`. Catch only that token's own `OperationCanceledException`, so the original failure still wins.

## Acceptance criteria

- [x] `ResetFailedStreamAsync` no longer passes `CancellationToken.None`. Its cleanup runs under a token that cancels after a bounded time, created through the handler's injected `TimeProvider`.
- [x] When that cleanup token cancels, the transfer still returns the original `HttpTransferException` exit code. The catch matches only the cleanup token's cancellation.
- [x] A new unit test in `Curl.Original.Protocol.Http.UnitTests` uses an HTTP/2 fake whose RST_STREAM write never completes, plus a fake `TimeProvider`. It shows the transfer returns exit 22 once the cleanup bound passes.
- [x] BL-5998's test `ExecuteAsync_TheTokenIsCancelledWhileAFailedStreamIsCancelled_KeepsExit22AndMarksTheTransferDone` still passes.
- [x] `dotnet build` is clean, and the fast tests pass with 100% line and branch coverage kept on the library.

## Notes

- Decided (ADR-1418): the cleanup limit is one second on the transfer's `TimeProvider`
  (`context.TimeProvider`, the handler's only injected clock). The timed-out path
  (`HttpProtocolHandler.ResponseExchange.cs`) is bounded the same way. No stream (HTTP/1.x)
  means no timer: a Console retry test counts every short timer.
- Bounding the reset alone still hung: closing the connection then wrote a GOAWAY with no
  token (`SessionHoldingConnection` here, `PoolEntry` in production). `Http2Session.ShutDownAsync`
  now writes nothing after a write was cancelled part way.
- `touches` widened to `Http2Session.cs` and `HttpProtocolHandler.ResponseExchange.cs`; no
  other task in Doing on `origin/work/dark-factory` named either.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 4e4a3ac8 e961c316 b9a65a29 2ee197b9 bd0dde9c.
- 2026-11-26: Doing -> Done. A failed or timed-out HTTP/2 or HTTP/3 transfer gives up its stream cleanup after one second and exits with its own code.
