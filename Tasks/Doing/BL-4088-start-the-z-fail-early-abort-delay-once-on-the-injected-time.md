---
id: BL-4088
title: Start the -Z --fail-early abort delay once, on the injected TimeProvider (production)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/ParallelRun.cs, Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4088 — Start the -Z --fail-early abort delay once, on the injected TimeProvider (production)

## Goal

Under `-Z --fail-early`, the run aborts `AbortDelay` (1 s) after the first failure only, and the delay runs on the runner's injected `TimeProvider`.

## Context

Split from BL-4062 (found by the shift-end review of 2026-10-28, diff `354cb7711...c4b167258`, CGR-294). `Curl.Console/ParallelRun.cs:202-206`: `RecordEnd` calls `abort.CancelAfter(AbortDelay)` for every failure, and `CancelAfter` restarts the timer, so later failures push the abort back indefinitely. `abort` (line 27) is a plain `new CancellationTokenSource()`, on the system timer.

## Acceptance criteria

- [ ] `RecordEnd` starts the abort delay only on the first failure that ends the run. A later failure does not move it.
- [ ] `abort` is created with the runner's `TimeProvider` (`new CancellationTokenSource(Timeout.InfiniteTimeSpan, timeProvider)` or equivalent), passed in through the constructor from `CurlCommandRunner`.
- [ ] `dotnet build` is clean, the fast tests pass without hanging, and `Curl.Console` still meets the four quality gates.

## Notes

Split from BL-4062. Continues the stashed work of BL-4062.
BL-4087 adds the fake-clock test. The stash's Cgr109Tests fail-early test hung intermittently on the fake clock, so existing tests that run a fail-early parallel run may need the fake clock advanced.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 2b0d65e4 4e425806 e1984c9d.
