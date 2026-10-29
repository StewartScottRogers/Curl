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
completed:
---
# BL-4074 — Cover MultiCallbackQueue.QueuedCallback.WaitUntilRun's cancelled-after-taken branch deterministically

## Goal

Every fast-test run covers lines 178-179 of `MultiCallbackQueue.cs` (`WaitUntilRun`'s path where the caller's token is cancelled after a perform has taken the callback), so Curl.Engine.UnitLibrary's coverage gate does not depend on thread timing.

## Context

BL-4072's Measure-CodeQuality.ps1 run for Curl.Engine.UnitLibrary (2026-10-29) reported `QueuedCallback.WaitUntilRun` at 83.33% line / 50% branch, lines 178 and 179 uncovered. `MultiCallbackQueueTests` has `Run_CancelledWhileThePerformRunsIt_WaitsForThatRun` and `Run_CancelledByTheCallback*` meant to reach it, but they depend on the caller being inside `ran.Wait` when the token is cancelled. Make the test wait for that event (or restructure the seam) instead of racing.

## Acceptance criteria

- [ ] A `MultiCallbackQueueTests` test reaches `WaitUntilRun`'s cancelled-after-taken return on every run, waiting for events rather than timing.
- [ ] Measure-CodeQuality.ps1 for Curl.Engine.UnitLibrary shows `WaitUntilRun` at 100% line and branch.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f723258e d025975b.
