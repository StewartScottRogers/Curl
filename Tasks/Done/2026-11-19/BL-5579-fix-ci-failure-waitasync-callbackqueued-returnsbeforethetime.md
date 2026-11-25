---
id: BL-5579
title: Fix CI failure WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5579 — Fix CI failure WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout on Windows

## Goal

`WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout` failed on Windows in CI run 44645606866 (https://github.com/StewartScottRogers/Curl/actions/runs/44645606866). First failing commit: 3365d21e.

    Assertion failed. Expected value to be less than the upper bound.

Lanes test only on Windows, so reproduce with `gh run view 44645606866 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

CI run took 32 s for a 4 MiB file transfer driven through the multi handle on a loaded Windows runner; the test asserted total elapsed < 30 s, which measures machine load, not a missed wake-up (the library queue signals Arrived correctly). Fix (test only, Curl.Engine.UnitTests/Cgr033Tests.cs): DriveUntilDoneAsync now waits 30 s per wait and asserts no single wait ran to its whole timeout (the real defect), with a 5-minute overall loop deadline; the total-elapsed assertion is gone. Production code unchanged, so no quality run. Linux/macOS: platform-neutral, no OS-specific API.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 2e4719cc.
- 2026-11-19: Doing -> Done. Test no longer asserts total elapsed time; asserts no wait ran to its whole timeout
