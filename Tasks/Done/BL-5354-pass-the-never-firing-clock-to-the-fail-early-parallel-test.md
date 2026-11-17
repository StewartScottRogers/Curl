---
id: BL-5354
title: Pass the never-firing clock to the fail-early parallel test BL-5353 meant to fix
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests/CurlCommandRunnerParallelTests.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5354 — Pass the never-firing clock to the fail-early parallel test BL-5353 meant to fix

## Goal

`RunAsync_FailEarlyAndATransferEndingBeforeTheAbortLands_CompletesIt` runs on a clock whose timers do not fire on their own, so `--fail-early`'s 1 s abort timer can no longer race the multi loop's poll on Linux, as BL-5353 said it did.

## Context

Found by the shift-end review of 2026-11-17 (ADR-0710), code-review pass over `46b988dd..c412666f`.

Commit 6437528ce8 (BL-5353) added an optional `TimeProvider? timeProvider = null` parameter to the private helper `RunAsync` in `Curl.Console.UnitTests/CurlCommandRunnerParallelTests.cs:534` and forwards it to the runner, but no test passes it. `RunAsync_FailEarlyAndATransferEndingBeforeTheAbortLands_CompletesIt` (line ~274) still calls `RunAsync(http, [...])` with the real clock. BL-5353's Notes and Log say "The test now runs on a clock whose timers never fire", which is not true of the code: the race that failed CI run 44077672653 on Linux (`/a` aborted with 42 after the test finished it) is still there, so the flake will come back and the Done task's notes misstate the code.

`ManualTimerTimeProvider` (`Curl.Console.UnitTests/ManualTimerTimeProvider.cs`) fires timers only when the test advances it - a fit for the fix.

## Acceptance criteria

- [x] `RunAsync_FailEarlyAndATransferEndingBeforeTheAbortLands_CompletesIt` passes a `TimeProvider` whose timers fire only when the test moves it (e.g. `new ManualTimerTimeProvider()`) to `RunAsync`, and still asserts exit 37 and stdout `1 37\n0 0\n`.
- [x] No other test in the class changes behaviour; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.
- [x] The `timeProvider` parameter of `RunAsync` has at least one caller (no dead parameter left behind).

## Notes

Check first whether the multi loop's own poll waits on the injected clock; if it does, a clock that never moves would hang the run, and the test must advance it past the poll but not the abort.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 21ea1363 bbde421c 2e86499b.
- 2026-11-17: Fixed in Curl.Console.UnitTests/CurlCommandRunnerParallelTests.cs; the multi poll does not wait on the injected clock, so no advance is needed. Fast tests green.
- 2026-11-17: Doing -> Done. Test now passes ManualTimerTimeProvider
