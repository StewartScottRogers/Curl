---
id: BL-3976
title: Wire ManagedMultiTransfer to real sockets, CURL_POLL_OUT and deadline timers for CGR-034 (C_20, C_74, C_75)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs, Curl.Engine.UnitTests/Cgr034Tests.cs]
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

## Log

- 2026-10-28: Created.
