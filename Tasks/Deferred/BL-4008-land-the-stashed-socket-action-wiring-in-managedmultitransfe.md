---
id: BL-4008
title: Land the stashed socket-action wiring in ManagedMultiTransfer and pin CGR-034 C_20, C_74, C_75
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs, Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs, Curl.Engine.UnitTests/Cgr034Tests.cs, Curl.Engine.UnitTests/SocketActionGatedConnectionTests.cs, Curl.Engine.UnitTests/SocketActionGatedTcpDialerTests.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4008 — Land the stashed socket-action wiring in ManagedMultiTransfer and pin CGR-034 C_20, C_74, C_75

## Goal

`curl_multi_socket_action` hands the application the transfer's real socket (or `TransferSocketWait.Socket` for a connection with no OS socket), registered for CURL_POLL_OUT then CURL_POLL_IN, with the timer armed from the transfer's real deadline, for the single-transfer, timer-only and timeout cases of CGR-034.

## Context

Split from BL-3989 (cost cap), itself split from BL-3976 and BL-3563 (CGR-034 in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; curl 8.22.0). Read the Notes of BL-3989, BL-3976 and BL-3563: ADR-0828 `TransferSocketWait`, `SocketActionGatedTcpDialer`, ADR-0955 `TakeSocket` and `SignalOn`, ADR-0964. Expected sequences from real curl: `C_20` one transfer `add T0 kick S1=2 T-1 S1=1 S1=4`; `C_74` timer only perform `T0 T-1`; `C_75` TIMEOUT_MS=600 against a silent server `T0 S=2 T400+ S=1 S=4 T-1` with result 28.

## Acceptance criteria

- [ ] `ManagedMultiTransfer` routes a socket action through `TransferSocketWait.SignalOn` and passes the real OS socket (or `TransferSocketWait.Socket`) to the socket callback with `CurlPoll.Out`, then `CurlPoll.In`.
- [ ] The timer callback gets the earliest transfer deadline, or -1 when none.
- [ ] Tests in `Curl.Engine.UnitTests/Cgr034Tests.cs` pin the `C_20`, `C_74` and `C_75` sequences above and fail before the fix and pass after it (ADR-0482).
- [ ] Upstream test530 still passes (a transfer waiting on an engine socket counts as ready in `CountReady`).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Engine.UnitTests`.

## Notes

- Split from BL-3989 (cost cap). The fallbacks (`FirstTransferSocket`, the 10 ms timer) stay until BL-3990. The upstream test582 hang and the quality gates are BL-4007.
- Continues the stashed work of BL-3989.
- The stashed work already pins `C_20`, `C_74` and `C_75` in `Cgr034Tests` and keeps both fallbacks. Apply it, keep CGR-150's `CloseIdleConnectionsForRoomAsync` beside it, and give its ADR a free number (0948 is taken; the earlier run renumbered it to ADR-0964, check it is still free).
- BL-3989's last run applied the stash (one conflict in `TransferSocketWait.cs`, resolved by keeping ADR-0955's `FirstEngineSocket` and dropping `DefaultConnectTimeout`), renumbered the ADR to ADR-0964, renamed the stash's no-OS-socket test to `..._TellsItsEngineSocketOutThenInThenRemove`, and made `ManagedMultiTransfer.CountReady` count a transfer waiting on an engine socket (`IsWaitingOnAnEngineSocket`) as ready (without it test530's `curl_multi_wait` slept 10 s). `Curl.Engine.UnitTests` was green (2738 passed). Upstream test582 hangs with this change; it is left to BL-4007, so do not put test582 on the ratchet-affecting path here beyond not making other cases worse.

- 2026-10-28 run (lane 13): ADR-0964 was taken by another lane's ADR, so the stash's ADR is now ADR-0969 (file, README row, citations in `ManagedMultiTransfer.cs` and `Cgr034Tests.cs`). `touches` gains `Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs`, whose expected sequence the stash changes (an extra `timer 0`); no task in Doing on `origin/work/dark-factory` names it. Measured: the stashed work builds clean, `Curl.Engine.UnitTests` is green (2751 passed), upstream test530 passes, upstream test582 fails (hangs past 20 s, twice).
- This task cannot be committed on its own: test582 is on `PassingUpstreamCases.txt` and `Curl.Conformance.UnitTests` runs in the fast suite, so landing the stash without the test582 fix turns CI red. The next claim must fix test582 in the same run (BL-4007's first criterion; BL-4007 depends on this task, so it cannot be a `depends-on` here without a cycle), then run the quality gate for `Curl.Engine.UnitLibrary` and the fast suite once.
- What a trace showed (a temporary trace in `SocketActionAsync` and `TellWaitedSocket`, since removed): the SFTP connection is in-memory, so the transfer waits on engine socket `0x50000001`; the transfer alternates `Out` and `In` and the app's actions follow correctly for about 1.1 s (~30 actions), then a `SocketActionAsync(0x50000001, In)` call never returns: no socket or timer callback follows it and no later action comes. So the hang is inside `MoveTransfersOnAsync` on that action, not in the app's loop. `Settle` is bounded by `SocketWaitSettleLimit` (200 ms), so look at what else blocks there: `RunQueuedCallbacks` running the upload's read callback (Lib582 reads its file through it) while `Settle`'s synchronous `Task.WaitAny` holds the app thread, or the transfer's in-memory continuations running inline on the caller's thread. Re-trace inside `MoveTransfersOnAsync` (before and after `RunQueuedCallbacks`, `MoveEveryTransferOn`, `SettleSocketWaitingTransfers`) to find which call does not return.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 3a1e0c49 1d22eb8b 6e4e49ca 34b11bef a0b1d3ee.
- 2026-10-28: Doing -> Backlog. Out of run budget: stash lands only together with the upstream test582 hang fix (test582 is on the ratchet in the fast suite); the trace shows a SocketActionAsync(In) on the engine socket never returning, see Notes
- 2026-10-28: Backlog -> Deferred. Split (cost cap) into BL-4012 and BL-4013
