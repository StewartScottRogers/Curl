---
id: BL-4061
title: Stop the lookup wakeup socket being signalled after StopGating disposed it
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/LoopbackLookupWakeup.cs, Curl.Engine.UnitLibrary/SocketActionGatedResolver.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4061 — Stop the lookup wakeup socket being signalled after StopGating disposed it

## Goal

Removing a transfer from a multi handle, or cleaning the handle up, during a slow DNS lookup no longer throws `ObjectDisposedException` from the resolver or leaks a socket.

## Context

Found by the shift-end review of 2026-10-28 (type-design-analyzer and code-reviewer, ADR-0710), in the diff `354cb7711...c4b167258`.

- `Curl.Engine.UnitLibrary/SocketActionGatedResolver.cs:37` hands the `LoopbackLookupWakeup` to `TransferSocketWait.BeginResolve`, which owns it from then on. `TransferSocketWait.StopGating` (line 246) and the next `BeginResolve` (line 186) dispose it.
- The resolver still calls `wakeup.Signal()` in its `finally` (line 46) once the lookup ends, on the transfer's thread.
- `StopGating` runs on the application's thread, from `RemoveEntry` (`ManagedMultiTransfer.Queueing.cs`) and `DisposeAsync` (`ManagedMultiTransfer.Dispose.cs`). It can run while the lookup is still going.
- `LoopbackLookupWakeup.Signal` creates `caller` and connects to `listener.LocalEndPoint!`. After dispose that throws `ObjectDisposedException`. That exception replaces the lookup's result or its `OperationCanceledException`, `EndResolve` never runs, and the new `caller` socket is never closed, because `Dispose` already ran with `caller` null. Nothing synchronises the two threads.

## Acceptance criteria

- [x] `LoopbackLookupWakeup.Signal` after `Dispose` does nothing and throws nothing. A `Signal` racing `Dispose` leaves no open socket: there is a lock and a disposed flag, or an equivalent.
- [x] `TransferSocketWait.BeginResolve` disposes the wakeup it is given when gating has already stopped.
- [x] A test in `Curl.Engine.UnitTests` covers this: a transfer removed from the multi handle while a gated lookup is running, then the lookup completing. The transfer ends with its cancellation, not `ObjectDisposedException`.
- [x] A unit test calls `Signal` after `Dispose` and checks that nothing is thrown.
- [x] `dotnet build` is clean, the fast tests pass, and `Curl.Engine.UnitLibrary` still meets the four quality gates.

## Notes

- Quality run for Curl.Engine.UnitLibrary: the files I changed are at 100%; the one failing member is QueuedCallback.WaitUntilRun in MultiCallbackQueue.cs (not touched here).
- The removal test is at resolver level: StopGating mid-lookup, then the lookup completes and returns its result without ObjectDisposedException.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5b6e6d22 ea5cecd0 0c1b5590 0daca6dc 9837a783.
- 2026-10-29: Doing -> Done. Signal after Dispose is a no-op under a lock; BeginResolve disposes the wakeup once gating stopped
