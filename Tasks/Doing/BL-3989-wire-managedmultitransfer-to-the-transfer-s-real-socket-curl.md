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

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 3a1e0c49 1d22eb8b 6e4e49ca 779790c0.
