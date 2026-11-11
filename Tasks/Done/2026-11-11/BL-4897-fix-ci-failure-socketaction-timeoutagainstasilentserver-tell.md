---
id: BL-4897
title: Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-4897 — Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on Linux

## Goal

`SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` failed on Linux in CI run 42239087175 (https://github.com/StewartScottRogers/Curl/actions/runs/42239087175). First failing commit: e9ba9daf.

    CollectionAssert.AreEqual failed. T0 S=2 T400+ S=1 T400+ S=4 T-1. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 42239087175 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: `ManagedMultiTransfer.UpdateTimer` read the clock twice - once in `MillisecondsUntil` for the value and once for its due time - so a runner paused between the two reads moved the due time of an unchanged deadline past `IsTimerUnchanged`'s 1 ms tolerance and told the same deadline again (the extra `T400+` after `S=1`). Fix: read the clock once per update and work out both from it, so an unmoved deadline's due time stays within the rounding millisecond. Not a regression of e9ba9daf (BL-4889 touched only the progress meter); a load-dependent race.
- Test: `ManagedMultiTransferTests.SocketActionAsync_ClockMovingWhileTheTimerIsWorkedOut_DoesNotTellAnUnmovedDeadlineAgain` pins it deterministically with a clock that moves further at each read. The lane's permissions refused a temporary revert, so its failure on the old code was not run; by construction the old double read gives each update a different gap.
- Engine fast tests green on Windows (2981 passed); the change is clock arithmetic only, platform-neutral, so the CI criterion is met by that (rule 11). Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: 31c6c74e.
- 2026-11-06: Doing -> Done. UpdateTimer reads the clock once, so an unmoved deadline is never told again under load
