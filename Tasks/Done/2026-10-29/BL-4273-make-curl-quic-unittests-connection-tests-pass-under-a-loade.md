---
id: BL-4273
title: Make Curl.Quic.UnitTests' connection tests pass under a loaded full fast run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Quic.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4273 — Make Curl.Quic.UnitTests' connection tests pass under a loaded full fast run

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

- [x] The tests named under Context wait on events or a fake `TimeProvider`, not on wall-clock time.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- **Already fixed by BL-4269 (1f1902ec1, ADR-1068).** Same tests and cause as BL-4269 and BL-4272: MSTest's non-cooperative `[Timeout]` blocked a pool thread per running test and starved the connection loops under load. BL-4269 made every timeout cooperative and every wait observe `TestContext.CancellationToken` or `HangGuard`; the tests' own waits are on events (`WaitUntilReceiveWaitsAsync`, `WaitUntilSentAsync`, `ManualTimerTimeProvider.WaitForPendingTimerAsync`). BL-4154's failing run predates that commit. No code change was needed here.
- Checked: no non-cooperative `[Timeout]` and no `CancellationToken.None` left in Curl.Quic.UnitTests; `DOTNET_ThreadPool_ForceMaxWorkerThreads=2 dotnet test Curl.Quic.UnitTests` passed 506 of 506 in 567 ms; `dotnet build` clean (0 warnings); full fast run exit 0, 72 test projects passed, Curl.Quic.UnitTests 506 of 506. Only the task file changed, so no quality run and nothing platform-specific.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d6801968 3d836d5e.
- 2026-10-29: Doing -> Done. QUIC connection tests wait on events and pass in the full fast run (fixed by BL-4269's cooperative timeouts)
