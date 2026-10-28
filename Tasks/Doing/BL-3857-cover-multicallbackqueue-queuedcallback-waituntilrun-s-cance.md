---
id: BL-3857
title: Cover MultiCallbackQueue.QueuedCallback.WaitUntilRun's cancelled-after-taken branch deterministically
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/MultiCallbackQueue.cs, Curl.Engine.UnitTests/MultiCallbackQueueTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3857 — Cover MultiCallbackQueue.QueuedCallback.WaitUntilRun's cancelled-after-taken branch deterministically

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no failing member: `MultiCallbackQueue.QueuedCallback.WaitUntilRun` is at 100% line and branch coverage from the fast tests.

## Context

BL-3583's quality run on 2026-10-26 (`Curl.Engine.UnitLibrary` at 99.97%) reported `QueuedCallback.WaitUntilRun(CancellationToken)` at `Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:165` with lines 178 and 179 uncovered: the branch where the token is cancelled after a perform has already taken the callback (`TryTake` false), so it waits for the run. BL-3562 added it (ADR-0901). The branch depends on timing, so the test must arrange the order deterministically (take the callback, then cancel, then run it), never by racing threads.

## Acceptance criteria

- [ ] A unit test in `Curl.Engine.UnitTests/MultiCallbackQueueTests.cs` drives the cancelled-after-taken branch without depending on thread scheduling.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no failing member.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: ed8828cc a526f176 779790c0.
