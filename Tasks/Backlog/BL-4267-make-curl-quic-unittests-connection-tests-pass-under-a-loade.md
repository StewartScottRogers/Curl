---
id: BL-4267
title: Make Curl.Quic.UnitTests' connection tests pass under a loaded full fast run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Quic.UnitTests]
requirement: none
created: 2026-10-29
completed:
---
# BL-4267 — Make Curl.Quic.UnitTests' connection tests pass under a loaded full fast run

## Goal

Curl.Quic.UnitTests passes in a full `dotnet test --filter "TestCategory!=Integration"` run on a loaded machine, not only alone.

## Context

On 2026-10-29 (lane 13, BL-4154, which does not touch QUIC) the full fast run failed 10 of 506 tests in
Curl.Quic.UnitTests after about 2 minutes each, among them `PeerIdleTimeout_ServerDeclaresNone_IsNull`,
`SendAndReceiveDatagramAsync_BothAdvertise_CarriesDatagramsBothWays`, `OpenBidirectionalStreamAsync_RequestAndResponse_CarriesBothWays`,
`Loop_ServerGoesSilent_SendsKeepAlivesThenFailsWithTheIdleTimeout` and `Loop_StreamChangeWhileTheReceiveWaits_WakesTheLoopToSendIt`.
Run alone right after, the project passed 506 of 506 in 35 s. The failures look like a real-time wait or timeout
that a loaded machine (nine lanes building) runs past; find it and make the tests wait on the event they need or a
fake `TimeProvider`.

## Acceptance criteria

- [ ] The tests named under Context wait on events or a fake `TimeProvider`, not on wall-clock time.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
