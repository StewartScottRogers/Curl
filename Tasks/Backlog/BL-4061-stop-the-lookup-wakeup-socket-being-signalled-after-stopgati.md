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
completed:
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

- [ ] `LoopbackLookupWakeup.Signal` after `Dispose` does nothing and throws nothing. A `Signal` racing `Dispose` leaves no open socket: there is a lock and a disposed flag, or an equivalent.
- [ ] `TransferSocketWait.BeginResolve` disposes the wakeup it is given when gating has already stopped.
- [ ] A test in `Curl.Engine.UnitTests` covers this: a transfer removed from the multi handle while a gated lookup is running, then the lookup completing. The transfer ends with its cancellation, not `ObjectDisposedException`.
- [ ] A unit test calls `Signal` after `Dispose` and checks that nothing is thrown.
- [ ] `dotnet build` is clean, the fast tests pass, and `Curl.Engine.UnitLibrary` still meets the four quality gates.

## Notes

## Log

- 2026-10-29: Created.
