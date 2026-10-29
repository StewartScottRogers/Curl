---
id: BL-3989
title: Wire ManagedMultiTransfer to the transfer's real socket, CURL_POLL_OUT then CURL_POLL_IN, for CGR-034 (C_20, C_74, C_75)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs, Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs, Curl.Engine.UnitTests/Cgr034Tests.cs, Curl.Engine.UnitTests/SocketActionGatedConnectionTests.cs, Curl.Engine.UnitTests/SocketActionGatedTcpDialerTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3989 — Wire ManagedMultiTransfer to the transfer's real socket, CURL_POLL_OUT then CURL_POLL_IN, for CGR-034 (C_20, C_74, C_75)

## Goal

`curl_multi_socket_action` hands the application the transfer's real socket (or `TransferSocketWait.Socket` for a connection with no OS socket), registered for CURL_POLL_OUT then CURL_POLL_IN, with the timer armed from the transfer's real deadline, for the single-transfer, timer-only and timeout cases of CGR-034.

## Context

Split from BL-3976 (cost cap), itself split from BL-3563 (CGR-034 in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; curl 8.22.0). Read the Notes of BL-3976 and BL-3563: ADR-0828 `TransferSocketWait`, `SocketActionGatedTcpDialer`, ADR-0955 `TakeSocket` and `SignalOn`. Expected sequences from real curl: `C_20` one transfer `add T0 kick S1=2 T-1 S1=1 S1=4`; `C_74` timer only perform `T0 T-1`; `C_75` TIMEOUT_MS=600 against a silent server `T0 S=2 T400+ S=1 S=4 T-1` with result 28.

## Acceptance criteria

- [ ] `ManagedMultiTransfer` routes a socket action through `TransferSocketWait.SignalOn` and passes the real OS socket (or `TransferSocketWait.Socket`) to the socket callback with `CurlPoll.Out`, then `CurlPoll.In`.
- [ ] The timer callback gets the earliest transfer deadline, or -1 when none.
- [ ] Tests in `Curl.Engine.UnitTests/Cgr034Tests.cs` pin the `C_20`, `C_74` and `C_75` sequences above and fail before the fix and pass after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3976 (cost cap). The fallbacks (`FirstTransferSocket`, the 10 ms timer) stay until BL-3990.
- Continues the stashed work of BL-3976.
- The stashed work already pins `C_20`, `C_74` and `C_75` in `Cgr034Tests` and keeps both fallbacks; apply it, keep CGR-150's `CloseIdleConnectionsForRoomAsync` beside it, and give its ADR a free number (0948 is taken).
- 2026-10-28 run (lane 3): this run applied the stash (one conflict in `TransferSocketWait.cs`, resolved by keeping ADR-0955's `FirstEngineSocket` and dropping `DefaultConnectTimeout` as the stash did) and renumbered its ADR to ADR-0964 (file, README row, citations in `ManagedMultiTransfer.cs` and `Cgr034Tests.cs`). Since ADR-0955 gives an in-memory connection an engine socket number, the stash's `SocketAction_ConnectionWithoutAnOperatingSystemSocket_TimeoutActionsFinishIt` was out of date; it is now `..._TellsItsEngineSocketOutThenInThenRemove`. `ManagedMultiTransfer.CountReady` now counts a transfer that waits on an engine socket (`IsWaitingOnAnEngineSocket`) as ready, because no poll can tell when such a socket is ready. Without that, upstream test530's `select_test` (a `curl_multi_wait`) slept its whole 10 s timeout and the case hung; test530 passes now. `Curl.Engine.UnitTests` is green (2738 passed).
- What is left: upstream test582 (`Lib582Run`, an SFTP upload over socket actions) still hangs past the conformance runner's 20 s with this change; it is on the ratchet and passes without it. Find why the SSH transfer never finishes under the socket-gated wait (likely an SSH read or write waiting in a direction the socket callback was not told, or `Waiting` being `None` while it computes), fix it, then run `Curl.Conformance.UnitTests` and the whole fast suite once, and run `Measure-CodeQuality.ps1` for `Curl.Engine.UnitLibrary`. No separate task was filed for this, because the work needs this task's stashed code.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 3a1e0c49 1d22eb8b 6e4e49ca 779790c0.
- 2026-10-28: Doing -> Backlog. Out of run budget: stash applied and test530 fixed, but upstream test582 (SFTP multi upload) still hangs under the socket-gated wait; see Notes
- 2026-10-28: Backlog -> Deferred. Split into BL-4006 (land the stashed wiring) and BL-4007 (test582 hang and quality gates)
