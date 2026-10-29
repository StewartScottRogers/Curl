---
id: BL-4267
title: Make Curl.Quic.UnitTests connection tests pass under full-solution load
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Quic.UnitTests]
requirement: none
created: 2026-10-29
completed:
---
# BL-4267 — Make Curl.Quic.UnitTests connection tests pass under full-solution load

## Goal

`Curl.Quic.UnitTests` passes in the full fast run (`dotnet test --filter "TestCategory!=Integration"`) on a loaded machine, as it does alone.

## Context

Found by BL-4110 on lane 10 (2026-10-29). In the full fast run, 10 tests failed after about 2 minutes each: SendDatagramAsync_Cancelled_QueuesNothing, SendAndReceiveDatagramAsync_BothAdvertise_CarriesDatagramsBothWays, PeerIdleTimeout_ServerDeclaresOne_IsItsMaxIdleTimeout, OpenBidirectionalStreamAsync_RequestAndResponse_CarriesBothWays, Abort_SendsResetStreamAndStopSending, OpenUnidirectionalStreamAsync_NoStreamAllowed_WaitsForMaxStreamsAndSendsStreamsBlocked, Loop_UnacknowledgedStreamData_IsProbedWhenTheLossDetectionTimerFires, Loop_StreamChangeWhileTheReceiveWaits_WakesTheLoopToSendIt, Loop_ServerGoesSilent_SendsKeepAlivesThenFailsWithTheIdleTimeout and WriteAsync_AfterStopSending_ThrowsAndTheStreamIsReset. Run alone straight after, all 505 passed in 14 s. The failures look like a wait on real time or on the thread pool that runs out under load: find that wait and make each test wait for the event it needs.

## Acceptance criteria

- [ ] The ten tests above wait for the events they need, not on wall-clock time or thread-pool scheduling.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
