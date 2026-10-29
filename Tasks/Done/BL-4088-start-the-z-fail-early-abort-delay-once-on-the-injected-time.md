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
completed: 2026-10-29
---
# BL-4088 — Start the -Z --fail-early abort delay once, on the injected TimeProvider (production)

## Goal

Under `-Z --fail-early`, the run aborts `AbortDelay` (1 s) after the first failure only, and the delay runs on the runner's injected `TimeProvider`.

## Context

Split from BL-4062 (found by the shift-end review of 2026-10-28, diff `354cb7711...c4b167258`, CGR-294). `Curl.Console/ParallelRun.cs:202-206`: `RecordEnd` calls `abort.CancelAfter(AbortDelay)` for every failure, and `CancelAfter` restarts the timer, so later failures push the abort back indefinitely. `abort` (line 27) is a plain `new CancellationTokenSource()`, on the system timer.

## Acceptance criteria

- [x] `RecordEnd` starts the abort delay only on the first failure that ends the run. A later failure does not move it.
- [x] `abort` is created with the runner's `TimeProvider` (`new CancellationTokenSource(Timeout.InfiniteTimeSpan, timeProvider)` or equivalent), passed in through the constructor from `CurlCommandRunner`.
- [x] `dotnet build` is clean, the fast tests pass without hanging, and `Curl.Console` still meets the four quality gates.

## Notes

Split from BL-4062. Continues the stashed work of BL-4062.
BL-4087 adds the fake-clock test. The stash's Cgr109Tests fail-early test hung intermittently on the fake clock, so existing tests that run a fail-early parallel run may need the fake clock advanced.

- Decision (sensible default): the abort delay is an `ITimer` that the first run-ending failure creates on the injected `TimeProvider` (`ParallelRun.FailEarly`). It is not `new CancellationTokenSource(Timeout.InfiniteTimeSpan, timeProvider)`, because that constructor registers an infinite timer on the clock at once. The fake clocks of four parallel runner tests (`CurlCommandRunnerRateTests` and the retry and referer tests) recorded that timer and failed. A timer created only when needed is the "equivalent" the criterion allows, and it exists only under `--fail-early`.
- `ParallelRunAbortDelayTests.RecordEnd_SecondFailureWithinTheDelay_DoesNotMoveTheAbort` (from the BL-4062 stash) landed here. It pins the first criterion on a fake clock, and BL-4087 keeps the rest of the fake-clock testing. The `Cgr109Tests` fail-early test now advances the fake clock past the 1 s delay. The earlier run had removed the clock advances from two `-m 2` timeout tests; those are restored.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Console`. `RecordEnd` reached complexity 12, so the fail-early start moved into `FailEarly()`, and after that `ParallelRun` meets every gate. The only failing member left is `CurlComposition.CreateTransferDispatch` (83.33% branch coverage). It was already failing, this task does not touch it, and BL-4083 covers it.
- The fast tests are green on Windows. The change adds no path, OS-error or certificate dependence, so it is platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 2b0d65e4 4e425806 e1984c9d.
- 2026-10-29: Doing -> Done. Abort delay starts once, on the injected TimeProvider; build clean, fast tests green, ParallelRun within the gates
