---
id: BL-4062
title: Start the -Z --fail-early abort delay once, on the injected TimeProvider
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/ParallelRun.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests]
requirement: none
created: 2026-10-29
completed:
---
# BL-4062 — Start the -Z --fail-early abort delay once, on the injected TimeProvider

## Goal

Under `-Z --fail-early`, the run aborts `AbortDelay` (1 s) after the first failure, however many failures follow it. The delay runs on the runner's `TimeProvider`.

## Context

Found by the shift-end review of 2026-10-28 (code-reviewer, ADR-0710), in the diff `354cb7711...c4b167258` (CGR-294).

- `Curl.Console/ParallelRun.cs:202-206`: `RecordEnd` calls `stopStarts.Cancel(); abort.CancelAfter(AbortDelay);` for every failure that ends the run.
- `CancellationTokenSource.CancelAfter` restarts its timer on each call. `IsAborted` stays false during the window, so each further failure moves the abort back again. With many transfers running, each failing within a second of the one before, the abort is pushed back indefinitely. curl aborts about one second after the first failure.
- `abort` is a plain `new CancellationTokenSource()` (line 27), so the delay runs on the system timer. Root CLAUDE.md: "Inject `TimeProvider` for anything time-dependent". Tests cannot drive it with a fake clock.

## Acceptance criteria

- [ ] `RecordEnd` starts the abort delay only on the first failure that ends the run. A later failure does not move it.
- [ ] `abort` is created with the runner's `TimeProvider` (`new CancellationTokenSource(Timeout.InfiniteTimeSpan, timeProvider)` or equivalent), passed in through the constructor.
- [ ] A test in `Curl.Console.UnitTests` uses a fake `TimeProvider`. It records two failures 600 ms apart and checks that `IsAborted` is true 1 s after the first.
- [ ] `dotnet build` is clean, the fast tests pass, and `Curl.Console` still meets the four quality gates.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 2b0d65e4 9b094dd8 9264385c 01556522.
- 2026-10-29: Doing -> Backlog. Ran out of time: Cgr109Tests fail-early test hangs intermittently on the fake clock and the full Console fast run hung; the fix is uncommitted in the working tree
- 2026-10-29: Backlog -> Deferred. Split into BL-4088 and BL-4087 (over its cost cap)
