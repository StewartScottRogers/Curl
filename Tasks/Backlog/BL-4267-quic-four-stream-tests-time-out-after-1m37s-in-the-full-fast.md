---
id: BL-4267
title: Quic: four stream tests time out after 1m37s in the full fast run but pass alone
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Quic.UnitTests]
requirement: none
created: 2026-10-29
completed:
---
# BL-4267 — Quic: four stream tests time out after 1m37s in the full fast run but pass alone

## Goal

`Curl.Quic.UnitTests` passes in the full fast run (`dotnet test --filter "TestCategory!=Integration"`) as it does alone.

## Context

Found by BL-4159's run on 2026-10-29 (lane 1, Windows, other lanes building at once). In the full fast run these four
failed after 1 m 37 s each, while the project alone passed all 506 tests in 435 ms:
`WriteAsync_AfterStopSending_ThrowsAndTheStreamIsReset`, `OpenBidirectionalStreamAsync_RequestAndResponse_CarriesBothWays`,
`OpenUnidirectionalStreamAsync_NoStreamAllowed_WaitsForMaxStreamsAndSendsStreamsBlocked` and `Abort_SendsResetStreamAndStopSending`.
The shared timing suggests a wait on real time or on thread-pool scheduling under load rather than a logic fault: look for
a test that waits for a timer or a background loop instead of the event it needs.

## Acceptance criteria

- [ ] The four tests wait for the event they need (a fake `TimeProvider` or a signalled task), not on real time or scheduling order.
- [ ] `dotnet build` is clean and the fast tests pass, `Curl.Quic.UnitTests` included.

## Notes

## Log

- 2026-10-29: Created.
