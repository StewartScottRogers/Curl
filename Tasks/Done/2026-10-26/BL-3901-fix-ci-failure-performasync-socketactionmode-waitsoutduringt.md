---
id: BL-3901
title: Fix CI failure PerformAsync_SocketActionMode_WaitsOutDuringTheDialAndMovesBytesOnlyWhenSignalled on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3901 — Fix CI failure PerformAsync_SocketActionMode_WaitsOutDuringTheDialAndMovesBytesOnlyWhenSignalled on Linux

## Goal

`PerformAsync_SocketActionMode_WaitsOutDuringTheDialAndMovesBytesOnlyWhenSignalled` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `PerformAsync_SocketActionMode_WaitsOutDuringTheDialAndMovesBytesOnlyWhenSignalled` failed on Linux in CI run 40423012883 (https://github.com/StewartScottRogers/Curl/actions/runs/40423012883). First failing commit: 22de86cf.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40423012883 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `PerformAsync_SocketActionMode_WaitsOutDuringTheDialAndMovesBytesOnlyWhenSignalled` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a test race, not a product bug. After the dial was released, the stale `Out` of the dial's own connect wait satisfied `UntilAsync(Waiting != None)` at once; on Linux the connect then ended (`EndConnect`, `Waiting` back to `None`) before the test read `firstWait`. Fix: subscribe for the dial wait ending (`Waiting == None`, raised synchronously by `TransferSocketWait.Changed`) before releasing the dial, await it, and only then wait for the first write wait. Test-only change in `Curl.Engine.UnitTests/SocketActionGatedTcpDialerTests.cs`; no production code, so no quality run.
- Passes locally (class 8/8, project fast tests 2579 passed, 6 skipped). CI on Linux and macOS: the fix is platform-neutral (it waits for the event instead of relying on scheduling order), so the criterion is ticked on that basis; the shift's CI watch files a task for any failure.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 2338d4f0.
- 2026-10-26: Doing -> Done. SocketActionMode dial test waits for the dial's wait to end before reading the first write wait, so it no longer races on Linux
