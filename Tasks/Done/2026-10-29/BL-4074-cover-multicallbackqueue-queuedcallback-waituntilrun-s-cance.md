---
id: BL-4074
title: Cover MultiCallbackQueue.QueuedCallback.WaitUntilRun's cancelled-after-taken branch deterministically
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/MultiCallbackQueueTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4074 — Cover MultiCallbackQueue.QueuedCallback.WaitUntilRun's cancelled-after-taken branch deterministically

## Goal

Every fast-test run covers lines 178-179 of `MultiCallbackQueue.cs` (`WaitUntilRun`'s path where the caller's token is cancelled after a perform has taken the callback), so Curl.Engine.UnitLibrary's coverage gate does not depend on thread timing.

## Context

BL-4072's Measure-CodeQuality.ps1 run for Curl.Engine.UnitLibrary (2026-10-29) reported `QueuedCallback.WaitUntilRun` at 83.33% line / 50% branch, lines 178 and 179 uncovered. `MultiCallbackQueueTests` has `Run_CancelledWhileThePerformRunsIt_WaitsForThatRun` and `Run_CancelledByTheCallback*` meant to reach it, but they depend on the caller being inside `ran.Wait` when the token is cancelled. Make the test wait for that event (or restructure the seam) instead of racing.

## Acceptance criteria

- [x] A `MultiCallbackQueueTests` test reaches `WaitUntilRun`'s cancelled-after-taken return on every run, waiting for events rather than timing.
- [x] Measure-CodeQuality.ps1 for Curl.Engine.UnitLibrary shows `WaitUntilRun` at 100% line and branch.

## Notes

- Cause: in `Run_CancelledWhileThePerformRunsIt_WaitsForThatRun` (and the two cancelled-by-the-callback tests) the callback could finish and set `ran` before the caller thread woke from `ran.Wait(token)`; `ManualResetEventSlim.Wait` then sees the event set and returns true without throwing, skipping lines 178-179.
- Fix (test only): the callback now waits until the caller has thrown its `OperationCanceledException` for the test's own token, observed through `AppDomain.FirstChanceException` filtered on that token (safe beside parallel tests), so the caller always goes through the catch with the callback already taken. No production change; the other two tests are left as they are.
- Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members, no coverage recheck needed; build clean, fast tests green (46 test projects passed). The test uses no paths, OS error text or timing, so it is platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f723258e d025975b.
- 2026-10-29: Doing -> Done. WaitUntilRun's cancelled-after-taken path is covered on every run: the test waits for the caller's cancellation throw instead of racing
