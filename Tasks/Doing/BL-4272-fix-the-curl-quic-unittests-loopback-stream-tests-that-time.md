---
id: BL-4272
title: Fix the Curl.Quic.UnitTests loopback stream tests that time out on a loaded machine
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Quic.UnitTests/QuicConnectionTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4272 — Fix the Curl.Quic.UnitTests loopback stream tests that time out on a loaded machine

## Goal

Curl.Quic.UnitTests passes on a machine running other builds, as it does on an idle one.

## Context

Found by BL-4194 (lane 5, 2026-10-29), whose change does not reach Curl.Quic: in the full fast run six `QuicConnectionTests` failed after about 1 m 39 s each (Abort_SendsResetStreamAndStopSending, WriteAsync_AfterStopSending_ThrowsAndTheStreamIsReset, SendAndReceiveDatagramAsync_BothAdvertise_CarriesDatagramsBothWays, OpenBidirectionalStreamAsync_RequestAndResponse_CarriesBothWays, Loop_StreamChangeWhileTheReceiveWaits_WakesTheLoopToSendIt, OpenUnidirectionalStreamAsync_NoStreamAllowed_WaitsForMaxStreamsAndSendsStreamsBlocked), and four still failed when the project ran alone with nine lanes building. Recent changes there: d25bb9059 (peer-initiated bidirectional streams) and 6c3416756 (DATAGRAM frames, BL-4222). Find whether a test waits on a timer or on scheduling instead of the event it needs.

## Acceptance criteria

- [ ] The six named tests wait for the event they need, not for time or scheduling, and pass in the full fast run.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 4fb887a9 3d836d5e.
