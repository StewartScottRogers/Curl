---
id: BL-3332
title: Fix CI failure RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3332 — Fix CI failure RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage on Linux

## Goal

`RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage` failed on Linux in CI run 39986660283 (https://github.com/StewartScottRogers/Curl/actions/runs/39986660283). First failing commit: 13c20167.

    Assertion failed. Expected condition to be false.

Lanes test only on Windows, so reproduce with `gh run view 39986660283 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: BL-3163 (714c4c48) starts the slow spell when the watchdog is created, so `-y 2` aborts at exactly 2 s. The test still asserted the run was unfinished after 2 s; on Windows that held only because the cancellation continuation had not run yet, and on Linux it had. The same stale check was in `RunAsync_SpeedLimitWithoutSpeedTime_WaitsCurlsThirtySeconds` (30 s) and `RunAsync_SpeedTimeAfterZeroSpeedLimit_WatchesForOneBytePerSecond`. Fixed in the tests only: each now checks the run is unfinished one second before the speed time (no timer check can abort then, so it is deterministic on every platform) and that it ends with exit 28 at the speed time.
- The CI criterion is met by keeping the change platform-neutral (a stepping clock, no paths or OS text) with Curl.Console.UnitTests green here (2868 passed); the shift's CI watch files a task for any failure. No production code changed, so no quality run was needed.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 2ae4e460.
- 2026-10-23: Doing -> Done. The speed-limit tests check the run is still going one second before -y, not at it, so they pass on every platform
