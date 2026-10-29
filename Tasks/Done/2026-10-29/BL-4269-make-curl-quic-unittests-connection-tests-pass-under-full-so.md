---
id: BL-4269
title: Make Curl.Quic.UnitTests connection tests pass under full-solution load
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Quic.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4269 — Make Curl.Quic.UnitTests connection tests pass under full-solution load

## Goal

`Curl.Quic.UnitTests` passes in the full fast run (`dotnet test --filter "TestCategory!=Integration"`) on a loaded machine, as it does alone.

## Context

Found by BL-4110 on lane 10 (2026-10-29). In the full fast run, 10 tests failed after about 2 minutes each: SendDatagramAsync_Cancelled_QueuesNothing, SendAndReceiveDatagramAsync_BothAdvertise_CarriesDatagramsBothWays, PeerIdleTimeout_ServerDeclaresOne_IsItsMaxIdleTimeout, OpenBidirectionalStreamAsync_RequestAndResponse_CarriesBothWays, Abort_SendsResetStreamAndStopSending, OpenUnidirectionalStreamAsync_NoStreamAllowed_WaitsForMaxStreamsAndSendsStreamsBlocked, Loop_UnacknowledgedStreamData_IsProbedWhenTheLossDetectionTimerFires, Loop_StreamChangeWhileTheReceiveWaits_WakesTheLoopToSendIt, Loop_ServerGoesSilent_SendsKeepAlivesThenFailsWithTheIdleTimeout and WriteAsync_AfterStopSending_ThrowsAndTheStreamIsReset. Run alone straight after, all 505 passed in 14 s. The failures look like a wait on real time or on the thread pool that runs out under load: find that wait and make each test wait for the event it needs.

## Acceptance criteria

- [x] The ten tests above wait for the events they need, not on wall-clock time or thread-pool scheduling.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- **Cause.** The tests' own waits were already on events. The wait that ran out was MSTest's non-cooperative `[Timeout]`, which blocks a thread-pool thread for each running test (method-level parallel, so one per worker). The connection loop, the handshake and `HangGuard` need free pool threads for their continuations and timer callbacks, and a pool on a nearly saturated machine barely grows. So the tests sat until the kernel-timed 120 s timeout, before the 60 s guard could fire. `DOTNET_ThreadPool_ForceMaxWorkerThreads=4` reproduces it every time: 6+ of the 34 `QuicConnection*` tests timed out at 120 s, including `PeerIdleTimeout_ServerDeclaresNone_IsNull`, which only handshakes and disposes. Load alone did not reproduce it (36 runs, 6 at once, `DOTNET_PROCESSOR_COUNT=2`, all passed).
- **Fix (ADR-1072).** Every `[Timeout(QuicTest.HangTimeoutMilliseconds)]` is now `CooperativeCancellation = true`, so MSTest awaits the test rather than blocking a thread. Every `CancellationToken.None` in the three async test files is now `TestContext.CancellationToken`, so the timeout still ends a hung test; the remaining waits are bounded by `HangGuard`. With 4 pool threads the whole project passes, 506 of 506, in under a second.
- Fast run: exit 0, 69 test projects passed. Only test code changed, so no `Measure-CodeQuality.ps1` run. The change is platform-neutral (no paths, OS text or timing), so CI on Linux and macOS should be unaffected.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 8cae939c 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Quic.UnitTests' async tests time out cooperatively and no longer starve the thread pool under full-solution load
