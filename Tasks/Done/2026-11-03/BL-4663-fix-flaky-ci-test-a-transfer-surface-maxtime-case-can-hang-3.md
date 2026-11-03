---
id: BL-4663
title: Fix flaky CI test: a transfer-surface MaxTime case can hang 30 s in the full run
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/TransferSurfaceCaseRun.cs, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MaxTimeDeadlineRun.cs, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Brokers/Amqp0/Amqp0TransferChecks.cs, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Brokers/Amqp1/Amqp1TransferChecks.cs, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Brokers/Kafka/KafkaTransferChecks.cs, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Brokers/Nats/NatsTransferChecks.cs, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Brokers/Stomp/StompTransferChecks.cs, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Processes/ProcessTransferRun.cs, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Realtime/RealtimeTransferChecks.cs, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage/StorageTransferChecks.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-03
---
## Goal

Every `MaxTime_ServerNeverAnswers_Exits28AtTheDeadline` case under `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/` finishes on its injected clock in the full fast run, never at the 30-second wait.

## Context

During BL-4626 (2026-11-02, lane 10), `dotnet test Curl.slnx --filter "TestCategory!=Integration"` failed one `MaxTime_ServerNeverAnswers_Exits28AtTheDeadline` case after `[30 s]` in `Curl.Conformance.Extended.UnitTests`. The project passed when run alone just after. The console output did not name the class, and nine classes have a test by that name (Mqtt5, Redis, Xmpp, and the Storage cases).

Likely cause: `TransferSurfaceCaseRun.RunPastMaxTimeAsync` advances `ManualClock` at most `seconds + 1` times with only `Task.Yield()` between steps, as soon as the server has seen curl's first byte. If curl's `-m` watchdog timer is created, or re-armed, only after the clock has already moved past the deadline, `ManualTimer.Change` schedules it relative to the moved clock. The loop then ends and nothing fires the timer, so `run.WaitAsync(30 s)` throws. Under load from the full run, the yields are not enough for curl to reach its timer. Fix: keep advancing until the run completes, with a bound on the number of steps rather than on seconds. Or wait for the timer to be registered first, for example by having `ManualClock` expose a `TimerCreated` signal. Check the Storage folder's runner for the same pattern.

## Acceptance criteria

- [x] `RunPastMaxTimeAsync` (and any copy of the pattern under `CurlSurface/Transfer/`) cannot end its advance loop before curl's `-m` timer has fired; a comment says why.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-11-03 (lane 6): The same advance loop (at most `-m` + 1 one-second steps, only `Task.Yield` between) was in eight more files under `CurlSurface/Transfer/` (Amqp0, Amqp1, Kafka, Nats, Stomp, Realtime and Storage checks, `ProcessTransferRun`). `touches` widened to them and the new helper, as the first criterion asks, after checking that no task in Doing on origin/work/dark-factory names them.
- Fix: the new `MaxTimeDeadlineRun.AdvanceUntilDoneAsync` advances the clock a second at a time until the run completes, bounded by 3000 steps rather than by `-m` seconds, with up to 10 ms of real time per step (`Task.WhenAny` with the run). Its remarks say why, and every copy calls it. Default taken: a step bound with a real pause rather than a `TimerCreated` signal, because it also covers a clock timer curl awaits after the watchdog fires (a shutdown delay), not only the watchdog's own.
- `MaxTimeWatchdog` (Curl.Core) creates its timer before connecting, and on the manual clock it fires at its due time, so the pinned "5000 milliseconds" text is unaffected by advancing further.
- Checks: `dotnet build Curl.slnx -warnaserror` clean; fast tests green in all 115 test projects (Curl.Conformance.Extended.UnitTests 1456 passed, 1 skipped); `dotnet format --verify-no-changes` clean. Only test code changed, so no Measure-CodeQuality run. Platform-neutral: no paths, OS text or certificates involved; CI on Linux and macOS is left to the shift's CI watch.

## Log

- 2026-11-02: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: fa8f2d3b 914c30cc.
- 2026-11-03: Doing -> Done. Every transfer-surface -m check advances its clock until curl's run ends, bounded by steps, not -m seconds
