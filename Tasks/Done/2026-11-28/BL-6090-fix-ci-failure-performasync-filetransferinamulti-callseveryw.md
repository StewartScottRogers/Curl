---
id: BL-6090
title: Fix CI failure PerformAsync_FileTransferInAMulti_CallsEveryWriteOnThePerformingThreadInsidePerform on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-6090 — Fix CI failure PerformAsync_FileTransferInAMulti_CallsEveryWriteOnThePerformingThreadInsidePerform on Windows

## Goal

`PerformAsync_FileTransferInAMulti_CallsEveryWriteOnThePerformingThreadInsidePerform` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `PerformAsync_FileTransferInAMulti_CallsEveryWriteOnThePerformingThreadInsidePerform` failed on Windows in CI run 46108135370 (https://github.com/StewartScottRogers/Curl/actions/runs/46108135370). First failing commit: b334fc27.

    Assertion failed. Expected value to be less than the upper bound.

Lanes test only on Windows, so reproduce with `gh run view 46108135370 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `PerformAsync_FileTransferInAMulti_CallsEveryWriteOnThePerformingThreadInsidePerform` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Evidence (gh run view 46108135370 --attempt 1 --log-failed; attempt 2 passed): the test took 35 s, and one curl_multi_wait took 31.7 s of its 30 s timeout.
- Cause, a library defect: a transfer's pool thread completed MultiCallbackQueue.Arrived and then blocked until a perform ran its callback. The waiter's continuation went to that blocked thread's own work queue, which another thread takes from only when the pool's shared queue is empty, so on a loaded machine only the wait's timeout ended it.
- Fix: the pending Arrived task is completed from a work item on the shared queue (preferLocal: false), a new pending task replaces it at each enqueue, and Arrived answers a completed task while a callback is queued.
- Regression test: MultiCallbackQueueTests.Arrived_CallbackAskedForOnAThreadPoolThreadWhileThePoolIsBusy_ResumesItsWaiter (DoNotParallelize; keeps the shared queue busy) failed on the old code after 10 s and passes now. Cgr033Tests is unchanged: its assertion caught a real defect.
- Build clean, fast tests green, dotnet format clean on the changed files, Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members.
- CI on Linux and macOS: the change and test use only the managed thread pool, no OS API, path or error text, and the fast tests are green here; the shift's CI watch files a task for any failure (lane rule on CI criteria).
- Done directly rather than through the /feature stages: a small bug fix in one class. No ADR: no design or behaviour decision.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 8761eeff.
- 2026-11-27: Doing -> Done. A multi wait is woken by a queued callback even on a busy thread pool, so it no longer runs to its whole timeout
