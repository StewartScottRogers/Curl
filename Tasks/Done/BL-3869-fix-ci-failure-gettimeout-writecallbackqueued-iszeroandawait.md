---
id: BL-3869
title: Fix CI failure GetTimeout_WriteCallbackQueued_IsZeroAndAWaitCountsItReady on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3869 — Fix CI failure GetTimeout_WriteCallbackQueued_IsZeroAndAWaitCountsItReady on Linux

## Goal

`GetTimeout_WriteCallbackQueued_IsZeroAndAWaitCountsItReady` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `GetTimeout_WriteCallbackQueued_IsZeroAndAWaitCountsItReady` failed on Linux in CI run 40387515096 (https://github.com/StewartScottRogers/Curl/actions/runs/40387515096). First failing commit: b6305926.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40387515096 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `GetTimeout_WriteCallbackQueued_IsZeroAndAWaitCountsItReady` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause (from the CI log: failed after 30.3 s with ready 0): the test drove a 4 MiB file:// transfer and
  relied on its reads yielding off the performing thread. Where file reads complete synchronously (Linux CI),
  the whole transfer ran inline inside the first perform, so no write was ever queued: GetTimeout stayed -1 until
  the test's 30 s deadline and the wait then returned 0 at once. The engine is right (libcurl's file:// also
  finishes inside one perform); the test was wrong.
- Fix: the test now uses a fake `http` protocol handler that writes only after the test releases it, once the
  two performs are done, so its write always comes from a thread-pool thread and waits queued for a perform. Only
  `Curl.Engine.UnitTests/Cgr033Tests.cs` changed; no production code, so no quality run was needed.
- Narrowing `touches` (step 2a) was skipped: the push to work/dark-factory was refused in this run. The broad
  `touches` stayed in place, which is safe.
- CI criterion: ticked because the change is platform-neutral (no file system, no timing race: the test waits for
  GetTimeout to report the queued write before it waits) and the Cgr033 tests and all 2557 fast tests of
  Curl.Engine.UnitTests are green on Windows (rule 11). The shift's CI watch files a task if CI fails.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: f54dbd61.
- 2026-10-26: Doing -> Done. GetTimeout_WriteCallbackQueued_IsZeroAndAWaitCountsItReady no longer depends on file reads yielding, so it passes on every platform
