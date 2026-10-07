---
id: BL-1355
title: Make upstream test1677 pass under a loaded machine, as it failed twice in lane integration
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1355 — Make upstream test1677 pass under a loaded machine, as it failed twice in lane integration

## Goal

Upstream case `test1677` in `Curl.Conformance.UnitTests` passes reliably while up to nine dark factory lanes build and test on the same machine.

## Context

- On 2026-10-03 lane 1 could not integrate BL-1333 (an FTP-only change) because the fast tests failed twice on `Curl.Conformance.UnitTests: test1677`. Lane 7 then ran the same change's fast tests twice with test1677 passing, so the failure depends on machine load, not on BL-1333.
- `UpstreamTestData/test1677.rawhttp` is an HTTP POST whose server reply carries both `content-length: 0` and `transfer-encoding: chunked`, served with `writedelay: 500` so the chunks arrive in separate packets. A timing-dependent loopback server under load is the likely cause: find how the conformance runner applies `writedelay` and what timeout or read assumption breaks when a write arrives late.

## Acceptance criteria

- [x] The cause of the intermittent test1677 failure is named under Notes, with the file and line.
- [x] The fix makes test1677 independent of machine load (no larger sleep or timeout as the only change).
- [x] `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

- Cause: `UpstreamCaseRunner.RunScreenedAsync` (`Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs`, line 107 before this change) built `new SwsHttpServerConnector(testCase)`, which runs the emulation on `TimeProvider.System`. test1677's `writedelay: 500` over a 215-byte reply is 11 writes, so `SwsHttpServerConnection.WaitUntilAsync` (`SwsHttpServerConnection.cs` line 210) slept through 11 real timer wakes, 5.2 s measured alone. Under nine lanes' load each wake comes late and the run outgrows `UpstreamConformanceTests`'s limits. Coalesced writes are not the cause: the case passes with `writedelay` 0, 5 and 60 (measured).
- Fix (ADR-0404): `CurlTimerOptions.AnyIn` looks for a curl timer that races the server (`-m`, `-y`, `-Y`, `--max-time`, `--connect-timeout`, `--speed-time`, `--speed-limit`, `--expect100-timeout`); without one the runner gives the emulation a `WaitSkippingTimeProvider`, which moves on by each wait at once. Writes keep their order and their separate reads; test1677 now runs in 0.24 s. No sleep or timeout was changed.
- Touches widened to `Curl.Conformance.UnitLibrary`: the runner builds the server, so the fix is there. No task in Doing on origin/work/dark-factory named it (checked 2026-10-03).
- Conformance run: 684 passed, 0 failed; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 0 failing members.
- Seen while measuring: test3035 (unlisted; curl's real `--retry-delay` waits) once outlasted the 30 s hang limit under coverage; filed as BL-1359.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. test1677's server writes no longer wait on the real clock: with no curl timer racing it, the sws emulation runs on a wait-skipping clock (ADR-0404)
