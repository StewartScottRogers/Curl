---
id: BL-4087
title: Test the -Z --fail-early abort delay on a fake TimeProvider
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-4088]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4087 — Test the -Z --fail-early abort delay on a fake TimeProvider

## Goal

A unit test proves `-Z --fail-early` aborts 1 s after the first failure, however late a second failure comes, using a fake `TimeProvider`.

## Context

Split from BL-4062. Depends on BL-4088, which injects the `TimeProvider` into `ParallelRun`'s abort source. Tests live in `Curl.Console.UnitTests`.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests` uses a fake `TimeProvider`, records two failures 600 ms apart and checks that `IsAborted` is true 1 s after the first.
- [x] The test does not hang or flake: it passes in 20 consecutive runs of the Console fast tests.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-4062.

- Added `ParallelRunTests.RecordEnd_SecondFailureUnderFailEarly_AbortsOneSecondAfterTheFirst` on the existing `ManualTimerTimeProvider`: failures at 0 ms and 600 ms; not aborted at 999 ms, aborted at 1000 ms, first failure kept.
- Console fast tests ran 20 times in a row, all green (3094 passed each). Test-only change, so no quality run was needed.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 26540cf8 8a182fd3 3d836d5e.
- 2026-10-29: Doing -> Done. A fake-clock test shows -Z --fail-early aborts 1 s after the first failure, whatever the second does
