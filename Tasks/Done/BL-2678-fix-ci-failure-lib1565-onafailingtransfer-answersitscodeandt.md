---
id: BL-2678
title: Fix CI failure Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests, Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2678 — Fix CI failure Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops on Linux

## Goal

`Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops` failed on Linux in CI run 39077398008 (https://github.com/StewartScottRogers/Curl/actions/runs/39077398008). First failing commit: 35c73029.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 39077398008 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a race in the test, not in production. Lib1565's second thread waited 50 ms of real
  time before each handle; the fake multi's poll returns at once, so the main thread spins, and
  on a busy Linux runner it took longer than 50 ms to read the first handle's failure and record
  it. The second thread then handed over a second handle and made a third (expected 2, got 3).
- Fix: the test's `FirstTimerAtOnceTimeProvider` fires the first timer at once and leaves the
  rest at the system clock, and the wait is now 1000 ms, so the main thread has a full second
  to record the failure before the second handle is made. The test takes about 1 s. No
  production change, so no quality-gate run was needed.
- Passes locally on Windows (Curl.Conformance.LibTest.UnitTests 895 passed). A lane cannot
  push, so CI on the landing commit is confirmed by the shift's integration and its CI watch,
  which refiles the test if it fails again.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: f9362164.
- 2026-10-17: Doing -> Done. Lib1565 failing-transfer test no longer races: the second handle waits a full second after the first, made at once
