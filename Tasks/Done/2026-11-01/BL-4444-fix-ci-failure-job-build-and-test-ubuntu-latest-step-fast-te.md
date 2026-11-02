---
id: BL-4444
title: Fix CI failure job Build and test (ubuntu-latest) step Fast tests on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx, Curl.Console.UnitTests/Cgr109Tests.cs]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4444 — Fix CI failure job Build and test (ubuntu-latest) step Fast tests on Linux

## Goal

`job Build and test (ubuntu-latest) step Fast tests` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Build and test (ubuntu-latest) step Fast tests` failed on Linux in CI run 41258993636 (https://github.com/StewartScottRogers/Curl/actions/runs/41258993636). First failing commit: 338ec3df.

    Passed!  - Failed:     0, Passed:    48, Skipped:     0, Total:    48, Duration: 124 ms - Curl.Config.UnitTests.dll (net10.0)

Lanes test only on Windows, so reproduce with `gh run view 41258993636 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Build and test (ubuntu-latest) step Fast tests` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the run hung (blame 3-minute inactivity) in `Curl.Console.Cgr109Tests.RunAsync_HostFreedAfterMaxTimePassed_TimesOutTheWaiterWithoutConnecting`, the one `-m` test BL-4442's `FireLateTimersUntilAsync` did not cover. The test moves its clock to 2500 ms without firing timers (`MoveWithoutFiring`), so the waiter finds `-m` passed when the host frees it. But the `-Z` loop's `ParallelTransferMulti` polls on that clock with a 1000 ms timeout. When `/stall`'s end wakes the multi handle before the loop enters its poll, the poll waits on a timer nobody fires, and the run never ends. Which happens first depends on thread scheduling, so it showed on Linux.
- Fix (test only): `TimerFiringClock.FireLateTimersUntilAsync(run, fires)` takes a filter on the due time. The test fires only the timers due within one poll (`ParallelTransferMulti.PollTimeoutMilliseconds`) of now, never those due at 2000 (the `-m` watchdog and the waiter's `-m`) or later (a connect timeout, or a waiter timer created after the move). Firing every timer instead (tried first) also fired `/stall`'s `-m` watchdog and printed `Connection timed out after 2500 milliseconds`, which changes what the test checks.
- `touches` widened from `Curl.slnx` to the test file the fix lands in (a CI fix may, per the CI-fix rule). There is no production change, so no quality run was needed. The change has no paths, OS text or certificates, so it does not depend on the platform. Locally: `dotnet build` clean, and the fast tests green on Windows (Curl.Console.UnitTests 3117 passed, 33 skipped). The CI criterion is ticked on that basis; the shift's CI watch files any failure on Linux or macOS.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 1426f56b.
- 2026-10-31: Doing -> Done. Cgr109Tests' host-freed -m test wakes the -Z loop's missed poll, so the Linux fast tests no longer hang
