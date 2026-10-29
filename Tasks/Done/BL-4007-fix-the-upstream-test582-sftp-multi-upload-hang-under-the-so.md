---
id: BL-4007
title: Fix the upstream test582 SFTP multi upload hang under the socket-gated wait and run the quality gates
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitTests/Cgr034Tests.cs, Curl.Engine.UnitTests/TransferSocketWaitTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4007 — Fix the upstream test582 SFTP multi upload hang under the socket-gated wait and run the quality gates

## Goal

Upstream test582 (`Lib582Run`, an SFTP upload over socket actions) finishes within the conformance runner's 20 s with BL-4008's socket-gated wait in place, and `Curl.Engine.UnitLibrary` stays inside the quality gates.

## Context

Split from BL-3989 (cost cap). BL-4008 lands the socket-gated wait; with it test582 hangs past 20 s, and it is on the ratchet and passes without it. Likely cause: an SSH read or write waiting in a direction the socket callback was not told, or `Waiting` being `None` while the transfer computes.

## Acceptance criteria

- [x] The cause of the test582 hang is found and fixed, with a focused unit test in `Curl.Engine.UnitTests` that fails before the fix and passes after it (ADR-0482).
- [x] Upstream test582 passes under `Curl.Conformance.UnitTests` and stays on the ratchet.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for the whole fast suite.
- [x] `Measure-CodeQuality.ps1` reports `Curl.Engine.UnitLibrary` inside every gate.

## Notes

- Split from BL-3989 (cost cap). Builds on BL-4008's landed code, which holds the stashed work; read BL-4008 and BL-3989's Notes first.
- 2026-10-28 run (lane 11): the test582 hang was found and fixed by BL-4016 (aa0f98b44): the pooled SFTP connection kept the finished transfer's `TransferSocketWait`, so `curl_multi_cleanup`'s close waited for a socket action that never came; `TransferSocketWait.StopGating` releases it. Its focused test is `TransferSocketWaitTests.StopGating_ReadInProgress_ReleasesItAndEveryLaterWait` (it does not compile without the fix). Measured here: upstream test582 passes by `CURL_UPSTREAM_CASE=582` and stays on the ratchet.
- Quality: the first run of `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` failed `TimerMilliseconds` (complexity 16), `TellWaitedSocket` (12) and `IsRunningAndWaitingOnItsSocket` (50% branch). Fix: the waiting predicates moved to `TransferSocketWait` (`IsWaitingOnItsSocket`, `IsWaitingWithoutSocket`, `IsWaitingOnAnEngineSocket`), each branch tested in `TransferSocketWaitTests` (added to touches; no task in Doing on origin/work/dark-factory names it); `TellChangedSocket` and `RunningTimerMilliseconds` split out; the `Perform` check dropped from the timer's all-waiting test, since a handle timeout other than 0 already means every running transfer's perform is in progress. Second run: 100% line, 100% branch, 0 failing, worst CRAP 10.
- Measured: `dotnet build` clean; the whole fast suite green. The change is platform-neutral (no paths, OS errors or certificates), so CI on Linux and macOS is left to the shift's CI watch.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 92f70f8a 951ec1ef 84dfbe80 31c325b2.
- 2026-10-28: Doing -> Done. test582 passes and Curl.Engine.UnitLibrary is inside every quality gate (waiting predicates on TransferSocketWait, timer and socket-tell methods split)
