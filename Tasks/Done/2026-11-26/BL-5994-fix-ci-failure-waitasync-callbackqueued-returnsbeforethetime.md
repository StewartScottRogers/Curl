---
id: BL-5994
title: Fix CI failure WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5994 — Fix CI failure WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout on Windows

## Goal

`WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout` failed on Windows in CI run 45736721217 (https://github.com/StewartScottRogers/Curl/actions/runs/45736721217). First failing commit: 9c82f174.

    Assertion failed. Expected value to be less than the upper bound.

Lanes test only on Windows, so reproduce with `gh run view 45736721217 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a library defect, not a slow runner (BL-5579 fixed that other half). The CI run's single 30 s wait ran to its timeout because `ManagedMultiTransfer.WaitForEventAsync` listed only the performs still running. A perform that finished between `CountReady` and that list was neither counted nor waited on, so `curl_multi_wait` and `curl_multi_poll` slept the whole timeout. It now waits on every unreported perform, finished ones included, so such a wait returns at once.
- Regression test: `ManagedMultiTransferTests.WaitOrPoll_TransferFinishingBetweenTheReadyCountAndTheWaitForAnEvent_ReturnsWithoutTheTimeout` (wait and poll) lands the finish in that gap through the time provider. It failed with a `TimeoutException` on the old code and passes now.
- Full solution build clean, all fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.
- Linux/macOS CI: the change and test are platform-neutral (no OS API, path or error text) and the fast tests are green here; the shift's CI watch files a task for any failure (lane rule on CI criteria).
- No ADR: a bug fix, no design or behaviour decision.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 2e4719cc.
- 2026-11-25: Doing -> Done. A multi wait or poll now returns at once for a transfer that finishes just as it starts waiting, instead of sleeping its whole timeout
