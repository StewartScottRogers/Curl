---
id: BL-5571
title: Fix CI failure RunQueued_TakingOneCallbackWhileAnotherIsQueued_KeepsTheArrivedSignalCompleted on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5571 — Fix CI failure RunQueued_TakingOneCallbackWhileAnotherIsQueued_KeepsTheArrivedSignalCompleted on Linux

## Goal

`RunQueued_TakingOneCallbackWhileAnotherIsQueued_KeepsTheArrivedSignalCompleted` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunQueued_TakingOneCallbackWhileAnotherIsQueued_KeepsTheArrivedSignalCompleted` failed on Linux in CI run 44618782776 (https://github.com/StewartScottRogers/Curl/actions/runs/44618782776). First failing commit: f00e90e6.

    Assertion failed. Expected condition to be true.

Lanes test only on Windows, so reproduce with `gh run view 44618782776 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunQueued_TakingOneCallbackWhileAnotherIsQueued_KeepsTheArrivedSignalCompleted` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: the test waited for the first callback to queue, then 500 ms, hoping the second caller had queued too; on a slow Linux runner it had not, so the first callback ran with nothing queued and the Arrived signal was already reset. Fix: MultiCallbackQueue.QueuedCount (HasQueued now uses it) and the test waits until two are queued. Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members. CI criterion met by keeping the test platform-neutral (no timing assumption).

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: c195ed96.
- 2026-11-19: Doing -> Done. Test waits until both callbacks are queued; QueuedCount added
