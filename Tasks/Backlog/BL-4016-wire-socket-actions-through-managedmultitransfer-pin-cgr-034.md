---
id: BL-4016
title: Wire socket actions through ManagedMultiTransfer, pin CGR-034 C_20, C_74, C_75 and keep test582 passing
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4015]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitTests/Cgr034Tests.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4016 — Wire socket actions through ManagedMultiTransfer, pin CGR-034 C_20, C_74, C_75 and keep test582 passing

## Goal

`curl_multi_socket_action` hands the application the transfer's real socket (or `TransferSocketWait.Socket` for a connection with no OS socket), registered for CURL_POLL_OUT then CURL_POLL_IN, with the timer armed from the transfer's real deadline, for the single-transfer, timer-only and timeout cases of CGR-034, with upstream test582 still passing.

## Context

Split from BL-4008 (cost cap); read its Notes, including the test582 trace. Expected sequences from real curl: `C_20` one transfer `add T0 kick S1=2 T-1 S1=1 S1=4`; `C_74` timer only perform `T0 T-1`; `C_75` TIMEOUT_MS=600 against a silent server `T0 S=2 T400+ S=1 S=4 T-1` with result 28.

## Acceptance criteria

- [ ] `ManagedMultiTransfer` routes a socket action through `TransferSocketWait.SignalOn` and passes the real OS socket (or `TransferSocketWait.Socket`) to the socket callback with `CurlPoll.Out`, then `CurlPoll.In`.
- [ ] The timer callback gets the earliest transfer deadline, or -1 when none.
- [ ] Tests in `Curl.Engine.UnitTests/Cgr034Tests.cs` pin the `C_20`, `C_74` and `C_75` sequences above and fail before the fix and pass after it (ADR-0482); `ManagedMultiTransferTests.SocketAction.cs` is updated for the extra `timer 0`.
- [ ] `ManagedMultiTransfer.CountReady` counts a transfer waiting on an engine socket as ready, and upstream test530 passes.
- [ ] Upstream test582 passes with the wiring (the SFTP multi-upload no longer hangs in `MoveTransfersOnAsync`), so the fast suite's ratchet stays green.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Engine.UnitTests` and `Curl.Conformance.UnitTests`.

## Notes

- Split from BL-4008 (cost cap). Continues the stashed work of BL-4008.
- The stash is 2ed39938f4b4211d07f28f1ceb009eede15f0c36; BL-4015 landed its `TransferSocketWait.cs` and test hunks (take `git show 2ed39938f4:<path>` or `git diff` against it for the rest). Apply the stash's `ManagedMultiTransfer.cs`, `Cgr034Tests.cs` and `ManagedMultiTransferTests.SocketAction.cs` hunks (BL-4015 lands the other files). Keep CGR-150's `CloseIdleConnectionsForRoomAsync` beside the wiring. The fallbacks (`FirstTransferSocket`, the 10 ms timer) stay until BL-3990.
- Write the stash's ADR with a free number (ADR-0969 last run; check), "Decided by Claude under Stewart's delegation", with its README row and citations in `ManagedMultiTransfer.cs` and `Cgr034Tests.cs`.
- The test582 hang: a `SocketActionAsync(0x50000001, In)` never returns after ~30 actions (~1.1 s). Re-trace inside `MoveTransfersOnAsync` (before and after `RunQueuedCallbacks`, `MoveEveryTransferOn`, `SettleSocketWaitingTransfers`); suspects are the upload read callback run under `Settle`'s synchronous `Task.WaitAny`, or in-memory continuations running inline on the caller's thread. `Settle` is bounded by `SocketWaitSettleLimit` (200 ms). BL-4007 keeps any remaining test582 work and the quality gates.

## Log

- 2026-10-28: Created.
