---
id: BL-3976
title: Wire ManagedMultiTransfer to real sockets, CURL_POLL_OUT and deadline timers for CGR-034 (C_20, C_74, C_75)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs, Curl.Engine.UnitTests/Cgr034Tests.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Counts.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs, Curl.Engine.UnitTests/Cgr148Tests.cs, Curl.Engine.UnitTests/SocketActionGatedConnectionTests.cs, Curl.Engine.UnitTests/SocketActionGatedTcpDialerTests.cs, Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3976 — Wire ManagedMultiTransfer to real sockets, CURL_POLL_OUT and deadline timers for CGR-034 (C_20, C_74, C_75)

## Goal

`curl_multi_socket_action` hands the application the transfer's real socket, registers it for CURL_POLL_OUT then CURL_POLL_IN, and arms the timer from the transfer's real deadline (-1 when none), for the single-transfer, timer-only and timeout cases of CGR-034.

## Context

Split from BL-3563 (CGR-034 in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; curl 8.22.0). Read BL-3563's Notes: they describe the design (ADR-0828 `TransferSocketWait`, `SocketActionGatedTcpDialer`, `ManagedMultiTransfer` wiring). Expected sequences from real curl: `C_20` one transfer `add T0 kick S1=2 T-1 S1=1 S1=4`; `C_74` timer only perform `T0 T-1`; `C_75` TIMEOUT_MS=600 against a silent server `T0 S=2 T400+ S=1 S=4 T-1` with result 28.

## Acceptance criteria

- [ ] `ManagedMultiTransfer` passes the real OS socket to the socket callback with `CurlPoll.Out`, then `CurlPoll.In`, and no longer uses `FirstTransferSocket` synthetic numbers.
- [ ] The timer callback gets the earliest transfer deadline, or -1 when none; `RunningTransferCheckInterval` (10 ms) is removed.
- [ ] Tests in `Curl.Engine.UnitTests/Cgr034Tests.cs` pin the `C_20`, `C_74` and `C_75` sequences above and fail before the fix and pass after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3563 (cost cap).
- Continues the stashed work of BL-3563.
- 2026-10-28 run (lane 4): applied stash b2bb31c0's work (one conflict in `ManagedMultiTransfer.cs` beside CGR-150's `CloseIdleConnectionsForRoomAsync`, kept both). With it the engine builds clean and the 151 multi, socket-action, `Cgr033`, `Cgr034` and `Cgr148` tests pass, `Cgr034Tests` pinning `C_20`, `C_74` and `C_75`. Renumbered its ADR from 0864 (taken by the file-URL ADR) to ADR-0948, with the README row and the citations in `ManagedMultiTransfer.cs` and `Cgr034Tests.cs`.
- What is left for criteria 1 and 2: the stash keeps `FirstTransferSocket` numbers and the 10 ms `RunningTransferCheckInterval` timer for transfers that have no OS socket - test-fake `IEasyTransfer`s and connections over in-memory dialing (`Lib530Run`, `Lib582Run`), which only the 10 ms timer brings back. Removing them first needs such connections to have a socket the application is told of: filed as BL-3981 and added to `depends-on`. Once it is Done, remove both fallbacks here and move the fake-transfer tests in `ManagedMultiTransferTests.SocketAction.cs`, `.Counts.cs`, `.Timers.cs` and `Cgr148Tests.cs` to the real-socket answers (added to `touches`; no task in Doing on origin/work/dark-factory names them). Measure-CodeQuality not run yet.

- 2026-10-28 (BL-3981's run, lane 4): BL-3981 landed `TransferSocketWait.TakeSocket` (an engine socket number from `0x50000001` for a connection with no OS socket, set by `SocketActionGatedTcpDialer` after each dial) and `SignalOn(socket, events)` (ADR-0955), so BL-3981 was dropped from `depends-on` to break the cycle: BL-3981's criterion 2 needs this task's wiring. Route a socket action through `SignalOn` and tell the application `TransferSocketWait.Socket` for every transfer. Note ADR-0948 is now taken by the PEM pass-phrase ADR, so this task's ADR needs another number.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 2ff2ee8c 934d3e8d 6e4e49ca 779790c0.
- 2026-10-28: Doing -> Backlog. Waits on BL-3981: connections with no OS socket need a socket before FirstTransferSocket and the 10 ms timer can go
- 2026-10-28: Backlog -> Deferred. Split into BL-3989 (wire real sockets, POLL_OUT then POLL_IN, deadline timer) and BL-3990 (remove FirstTransferSocket and the 10 ms timer)
