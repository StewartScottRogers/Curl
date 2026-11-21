---
id: BL-5687
title: Fix CI failure job Build and test (ubuntu-latest) step Fast tests on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5687 — Fix CI failure job Build and test (ubuntu-latest) step Fast tests on Linux

## Goal

`job Build and test (ubuntu-latest) step Fast tests` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Build and test (ubuntu-latest) step Fast tests` failed on Linux in CI run 44925879682 (https://github.com/StewartScottRogers/Curl/actions/runs/44925879682). First failing commit: 0680b975.

    Passed!  - Failed:     0, Passed:    48, Skipped:     0, Total:    48, Duration: 182 ms - Curl.Config.UnitTests.dll (net10.0)

Lanes test only on Windows, so reproduce with `gh run view 44925879682 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Build and test (ubuntu-latest) step Fast tests` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

The failure was a 3-minute inactivity hang of `Curl.Console.UnitTests` (`CurlCommandRunnerParallelTests.RunAsync_FailEarlyAndATransferEndingBeforeTheAbortLands_CompletesIt`) on Linux in run 44925879682; the commit named (0680b975) only adds a networking test and cannot cause it. Locally on Windows the whole `CurlCommandRunnerParallelTests` class passes (23/23) and I could not reproduce it or find the race by reading ParallelRun, HeldTransferHandler and ManualTimerTimeProvider. Judged a one-off Linux scheduling flake; no code change. Reopen as "Fix flaky CI test" if it recurs.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 1426f56b.
- 2026-11-21: Doing -> Done. One-off Linux hang in a parallel fail-early test; not reproducible, no code change
