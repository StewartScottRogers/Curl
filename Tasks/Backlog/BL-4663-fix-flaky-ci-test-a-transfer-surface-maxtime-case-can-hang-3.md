---
id: BL-4663
title: Fix flaky CI test: a transfer-surface MaxTime case can hang 30 s in the full run
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/TransferSurfaceCaseRun.cs]
requirement: none
created: 2026-11-02
completed:
---
## Goal

Every `MaxTime_ServerNeverAnswers_Exits28AtTheDeadline` case under `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/` finishes on its injected clock in the full fast run, never at the 30-second wait.

## Context

During BL-4626 (2026-11-02, lane 10), `dotnet test Curl.slnx --filter "TestCategory!=Integration"` failed one `MaxTime_ServerNeverAnswers_Exits28AtTheDeadline` case after `[30 s]` in `Curl.Conformance.Extended.UnitTests`. The project passed when run alone just after. The console output did not name the class, and nine classes have a test by that name (Mqtt5, Redis, Xmpp, and the Storage cases).

Likely cause: `TransferSurfaceCaseRun.RunPastMaxTimeAsync` advances `ManualClock` at most `seconds + 1` times with only `Task.Yield()` between steps, as soon as the server has seen curl's first byte. If curl's `-m` watchdog timer is created, or re-armed, only after the clock has already moved past the deadline, `ManualTimer.Change` schedules it relative to the moved clock. The loop then ends and nothing fires the timer, so `run.WaitAsync(30 s)` throws. Under load from the full run, the yields are not enough for curl to reach its timer. Fix: keep advancing until the run completes, with a bound on the number of steps rather than on seconds. Or wait for the timer to be registered first, for example by having `ManualClock` expose a `TimerCreated` signal. Check the Storage folder's runner for the same pattern.

## Acceptance criteria

- [ ] `RunPastMaxTimeAsync` (and any copy of the pattern under `CurlSurface/Transfer/`) cannot end its advance loop before curl's `-m` timer has fired; a comment says why.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-02: Created.
