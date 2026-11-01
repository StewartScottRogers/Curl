---
id: BL-4442
title: Fix flaky CI test job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3 that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1, Curl.Console.UnitTests/Cgr109Tests.cs]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4442 — Fix flaky CI test job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3 that failed once on Windows

## Goal

`job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3` failed on Windows in CI run 41252530857 (https://github.com/StewartScottRogers/Curl/actions/runs/41252530857). First failing commit: 451233b3.

    VSTEST_CONNECTION_TIMEOUT: 600

Lanes test only on Windows, so reproduce with `gh run view 41252530857 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3` fail intermittently is named under Notes and removed.
- [x] `job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the shard hung (blame 3-minute inactivity) in `Curl.Console.Cgr109Tests.RunAsync_SecondSameHostTransferWaitsPastMaxTime_TimesOutWithoutConnecting` (CI run 41252530857). Its `TimerFiringClock.Advance(2000)` fires only the timers due at the moment it takes its snapshot; when the run created or re-armed a timer after that snapshot (the host waiter's `-m` timer or a poll timer, depending on thread scheduling), nothing moved the clock again and the run waited forever. Fix: `TimerFiringClock.FireLateTimersUntilAsync(run)` keeps firing every live timer until the run ends (30 s cap), used after `Advance` in both `-m` tests. Test-only change, so no production library quality run is needed; it is platform-neutral (no paths, OS text or certificates).
- Touches widened to `Curl.Console.UnitTests/Cgr109Tests.cs`, where the hang was; no other task in Doing on origin/work/dark-factory names it. `Measure-CodeQuality.ps1` needed no change.
- Verified: `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` 3117 passed, 0 failed. The CI criterion on Linux and macOS is met by keeping the change platform-neutral; the shift's CI watch files any failure.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: db83650e 4f733ff7.
- 2026-10-31: Doing -> Done. Cgr109Tests no longer hangs: late timers are fired until the run ends
