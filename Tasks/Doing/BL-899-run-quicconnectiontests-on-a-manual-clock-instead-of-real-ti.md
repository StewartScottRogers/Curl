---
id: BL-899
title: Run QuicConnectionTests on a manual clock instead of real time
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Quic.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-899 — Run QuicConnectionTests on a manual clock instead of real time

## Goal

The tests in `QuicConnectionTests` that depend on timers (idle timeout, keep-alive, loss
detection) advance a `ManualTimerTimeProvider` instead of waiting on the real clock, so
they pass the same way on a machine saturated by six lanes.

## Context

2026-09-29: `Loop_ServerGoesSilent_SendsKeepAlivesThenFailsWithTheIdleTimeout` failed once
inside a full fast-test run on a parked lane branch (BL-738) and passed 15 of 15 runs on
its own. It builds a `QuicConnection` on a live channel with a 400 ms idle timeout, and
`QuicTestLiveChannel.WaitUntilAsync` polls with `Task.Delay(1)`. `CLAUDE.md` says to
inject `TimeProvider` for anything time-dependent, and `QuicConnection` already takes
one. The test project already has `ManualTimerTimeProvider`.

## Acceptance criteria

- [ ] Every `QuicConnectionTests` test that waits on a timer drives it with a
      `ManualTimerTimeProvider` passed to `QuicConnection`, and asserts the same outcome
      as before.
- [ ] No test in `QuicConnectionTests` relies on a wall-clock window to pass.
- [ ] `dotnet test Curl.Quic.UnitTests` passes 20 runs in a row, and the fast tests pass.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
