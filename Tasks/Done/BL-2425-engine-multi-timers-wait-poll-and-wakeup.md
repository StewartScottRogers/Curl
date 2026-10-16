---
id: BL-2425
title: Engine: multi timers, wait, poll and wakeup
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2424]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2425 — Engine: multi timers, wait, poll and wakeup

## Goal

The engine multi has `Timeout`, `Wait`, `Poll` and `Wakeup`, driven by an injected `TimeProvider`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests with a fake time provider show the timeout value, a wakeup ending a wait, and poll with extra file descriptors.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan, as built (ADR-0596): `ManagedMultiTransfer` takes the engine's `TimeProvider` and an internal `IWaitSocketPoller`. `GetTimeout` is 0 while a perform has work, else the first running transfer's `CURLOPT_TIMEOUT` left (ms, rounded up) from the multi perform that started it, else -1. `WaitAsync`/`PollAsync` end on a completed perform, an extra socket event, or the timeout capped by `GetTimeout`; wait with nothing to wait on returns at once, poll waits the whole timeout; `Wakeup` is sticky like libcurl's wakeup socket pair.
- Correction to the plan: the managed engine owns no OS poll set, so extra sockets are re-checked every `SocketPollInterval` (10 ms) through `SocketWaitPoller` (`Socket.Poll` with no wait on a non-owning `Socket`). Default chosen: 10 ms keeps latency low without spinning.
- Tests: `ManagedMultiTransferTests.Timers.cs` (20 tests) on a new `TimerManualTimeProvider` fake whose timers fire on `Advance`; `SocketWaitPollerTests` on a loopback socket that sends no byte. The four members left the NotSupported test.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reported one failing member (a `WaitEnds` lambda's unreachable null branch); replaced with `entries.TrueForAll(entry => entry.Done)`, which has no branch. Build clean; fast tests: 41 projects passed, 0 failed (Curl.Engine.UnitTests 1096 passed). `PassingUpstreamCases.txt` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. The managed multi handle answers curl_multi_timeout from its transfers' timeouts and waits, polls and wakes up on the engine's TimeProvider, extra sockets included (ADR-0596)
