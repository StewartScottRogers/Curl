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
completed: 2026-10-29
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

- [x] The three tests above count only real sockets and pass on the current code.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Done directly, without the full `/feature` stages: the task changes tests only, and the Context already gives the design.
- `ManagedMultiTransferTests.RealSockets.cs` adds `SocketHoldingTransfer`, which wraps a `ManagedEasyTransfer`, takes a given socket number on its `TransferSocketWait`, sets `SocketTaken` and then waits (`UntilReadable`, `UntilCancelled` or `NotAtAll`). It also adds `HoldingSocket`, which sets the URL, HTTP version and `PipeWait` on the inner handle. It is a separate type from SocketAction.cs's private `WrappingEasyTransfer` because this task may not touch that file.
- The three tests now also assert which socket they saw: the told list pairs each transfer with its socket (0x1001, 0x1002), and the wait sockets begin with the transfer's real socket. So they fail if a synthetic `FirstTransferSocket` number is counted. The transfers are declared before the multi handle, so the handle is disposed first and cancels the waits that run until cancelled. The told test finishes the first transfer with a socket action on its socket, with `SocketWaitSettleLimit` raised to 30 s as in the SocketAction test (BL-4050). `PipeWaiting` lost its last caller and was removed.
- The test changes are platform-neutral: they use no paths, OS errors or certificates, and the pipe-wait test waits for `SocketTaken` before it reads the wait sockets.
- One full fast run failed in Curl.Protocol.Ssh.UnitTests (`ExecuteAsync_TracedQuoteCommandsBeforeAndAfter_WritesTheQuoteStates`, a project this task does not touch), and the next full run passed everywhere. Filed as BL-4071.
- `dotnet format --verify-no-changes` reports import ordering only in files this task does not touch; the files it changed are clean.


## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a082df36 779790c0.
- 2026-10-29: Doing -> Done. The multi handle's limit, pipe-wait and start tests drive transfers holding real sockets
