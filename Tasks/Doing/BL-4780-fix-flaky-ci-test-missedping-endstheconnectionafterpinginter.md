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
completed:
---
# BL-4780 — Fix flaky CI test MissedPing_EndsTheConnectionAfterPingIntervalPlusPingTimeoutOnTheInjectedClock

## Goal

`MissedPing_EndsTheConnectionAfterPingIntervalPlusPingTimeoutOnTheInjectedClock` passes every time in the full fast-test run, not only when run alone.

## Context

Found by BL-4755's run on 2026-11-03: in `dotnet test --filter "TestCategory!=Integration"` it failed in `Curl.Conformance.Extended.UnitTests` (2 s), then passed when run alone with `--filter FullyQualifiedName~MissedPing_EndsTheConnection`. The case is in `Curl.Conformance.Extended.UnitTests/SocketIo/SocketIoEngineIoCases.cs`. Likely the test reads its result before the concurrent work it starts has reached the event it waits on (a timer advanced on the injected clock before the connection's ping wait was armed), which method-level parallelism under load exposes. Make it wait for the event it needs instead of relying on scheduling.

## Acceptance criteria

- [ ] The test waits for the event it depends on (the ping wait armed, the connection ended) before it advances the clock or reads the result, with no wall-clock sleep.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 740dd9e0 9742157d.
