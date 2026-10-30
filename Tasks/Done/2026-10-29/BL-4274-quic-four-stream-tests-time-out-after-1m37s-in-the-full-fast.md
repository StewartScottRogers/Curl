---
id: BL-4274
title: Quic: four stream tests time out after 1m37s in the full fast run but pass alone
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Quic.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4274 — Quic: four stream tests time out after 1m37s in the full fast run but pass alone

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

- [x] The four tests wait for the event they need (a fake `TimeProvider` or a signalled task), not on real time or scheduling order.
- [x] `dotnet build` is clean and the fast tests pass, `Curl.Quic.UnitTests` included.

## Notes

- **Already fixed by BL-4269 (1f1902ec1, ADR-1072); no code change.** The 1 m 37 s failures were the non-cooperative MSTest `[Timeout]` starving the thread pool, which BL-4269 diagnosed and fixed. BL-4159's run started from a checkout before that commit. The four tests already wait on events: `QuicTestLiveChannel.WaitUntilSentAsync` (wakes on each datagram the client sends), awaited tasks, `ManualTimerTimeProvider` as the clock, `TestContext.CancellationToken` and `HangGuard`. None waits on real time or on scheduling order.
- Checked: `DOTNET_ThreadPool_ForceMaxWorkerThreads=4 dotnet test Curl.Quic.UnitTests`: 506 of 506 passed in 453 ms. Full fast run: exit 0, 73 test projects passed, Curl.Quic.UnitTests 506 of 506 in 754 ms. Only the task file changed, so no Measure-CodeQuality run.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3b34ea0b 172eee31.
- 2026-10-29: Doing -> Done. Quic stream tests already fixed by BL-4269; verified under a 4-thread pool and the full fast run
