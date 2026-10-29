---
id: BL-4070
title: Drive the multi handle's limit, pipe-wait and start tests over transfers holding real sockets for CGR-034
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/ManagedMultiTransferTests.Limits.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.RealSockets.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4070 — Drive the multi handle's limit, pipe-wait and start tests over transfers holding real sockets for CGR-034

## Goal

The multi handle's connection-limit, pipe-wait and start tests that count told or waited sockets drive transfers that hold a real socket, so they pass with and without the engine's synthetic transfer sockets (`FirstTransferSocket`).

## Context

Prerequisite of BL-4068 (CGR-034), which drops the synthetic socket numbers a running `ControlledEasyTransfer` (no `TransferSocketWait`) is given. These tests count those numbers and fail once they go (measured on BL-4068's stashed work):

- `ManagedMultiTransferTests.Limits.cs`: `PerformAsync_PendingTransfer_IsNotToldToTheSocketCallbackUntilItStarts` (`toldWhileHeld` 1) and every row of `GetWaitSockets_PipeWaitingTransferToARunningOneSOrigin_HoldsNoSocketOfItsOwnUnderMultiplex` (counts 2 and 3).
- `ManagedMultiTransferTests.cs`: `PerformAsync_TransferFinishedAsItStarts_IsStillRunningAfterThatCallAndDoneAfterTheNext` (`socketsAfterStart` 2).

Drive them over a wrapped `ManagedEasyTransfer` (as `ManagedMultiTransferTests.SocketAction.cs`'s `WrappingEasyTransfer`) whose perform calls `TransferSocketWait.TakeSocket` with a distinct number and then waits (`WaitUntilReadableAsync` with a socket callback; `Task.Delay(Timeout.InfiniteTimeSpan, token)` without, waiting for a `TaskCompletionSource` set after `TakeSocket` before reading the sockets). Put any shared helper in the new `ManagedMultiTransferTests.RealSockets.cs`; the pipe-wait test needs the wrapped transfer's URL, HTTP version and `PipeWait` options set on the inner handle. Do not touch `SocketAction.cs` or `Counts.cs` (BL-4068's stash changes them).

## Acceptance criteria

- [ ] The three tests above count only real sockets and pass on the current code.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes


## Log

- 2026-10-29: Created.
