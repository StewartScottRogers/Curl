---
id: BL-4780
title: Fix flaky CI test MissedPing_EndsTheConnectionAfterPingIntervalPlusPingTimeoutOnTheInjectedClock
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests/SocketIo/SocketIoEngineIoCases.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4780 — Fix flaky CI test MissedPing_EndsTheConnectionAfterPingIntervalPlusPingTimeoutOnTheInjectedClock

## Goal

`MissedPing_EndsTheConnectionAfterPingIntervalPlusPingTimeoutOnTheInjectedClock` passes every time in the full fast-test run, not only when run alone.

## Context

Found by BL-4755's run on 2026-11-03: in `dotnet test --filter "TestCategory!=Integration"` it failed in `Curl.Conformance.Extended.UnitTests` (2 s), then passed when run alone with `--filter FullyQualifiedName~MissedPing_EndsTheConnection`. The case is in `Curl.Conformance.Extended.UnitTests/SocketIo/SocketIoEngineIoCases.cs`. Likely the test reads its result before the concurrent work it starts has reached the event it waits on (a timer advanced on the injected clock before the connection's ping wait was armed), which method-level parallelism under load exposes. Make it wait for the event it needs instead of relying on scheduling.

## Acceptance criteria

- [x] The test waits for the event it depends on (the ping wait armed, the connection ended) before it advances the clock or reads the result, with no wall-clock sleep.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The client arms its heartbeat (`heartbeat.CancelAfter`) on the open packet, before it sends `40`, so the existing wait for the first client packet already means the ping wait is armed. The flake was the end: after 2.5 s on the injected clock the test gave the run only about 5 x 10 ms of wall time to finish once the heartbeat fired. The test now advances the clock 2000 ms (pingInterval + pingTimeout) in one step and awaits the run itself (`WaitAsync`, 30 s cap), with no wall-clock sleep. Build clean and the fast tests green (115 assemblies, 0 failed). Test-only change, so no quality run was needed.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 740dd9e0 9742157d.
- 2026-11-03: Doing -> Done. Test advances the clock to the heartbeat timeout and awaits the run
