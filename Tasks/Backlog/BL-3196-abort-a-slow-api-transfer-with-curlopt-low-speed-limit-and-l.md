---
id: BL-3196
title: Abort a slow API transfer with CURLOPT_LOW_SPEED_LIMIT and LOW_SPEED_TIME
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferTimers.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs, Curl.Engine.UnitTests/LowSpeedLimitTests.cs, Libcurl.UnitTests/Options/LowSpeedOptionTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3196 — Abort a slow API transfer with CURLOPT_LOW_SPEED_LIMIT and LOW_SPEED_TIME

## Goal

`CURLOPT_LOW_SPEED_LIMIT` with `LOW_SPEED_TIME` aborts a transfer slower than the limit for that long with `CURLE_OPERATION_TIMEDOUT` (28), as upstream does.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 25). Report 25: the API options do nothing. The CLI's `-y`/`-Y` (report 3) were fixed by BL-3163 (Done); find how that fix reaches the transfer and make the API options use the same path (the engine's timers are the likely seam). Use the injected `TimeProvider` (a fake clock), no real waiting.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests/LowSpeedLimitTests.cs` drives a fake-clock transfer below the limit for longer than LOW_SPEED_TIME and expects exit 28 with upstream's message text.
- [ ] A transfer above the limit, and one with LOW_SPEED_TIME 0, completes normally.
- [ ] `Libcurl.UnitTests/Options/LowSpeedOptionTests.cs` sets both options through `CurlEasy` and expects 28.
- [ ] A test adds the same slow handle to a multi handle (`CurlMulti`) and expects it to finish with 28, as upstream does.
- [ ] BL-3163's CLI tests still pass. Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- Cause (2026-10-22, lane 7): `Libcurl`'s setters already put `CURLOPT_LOW_SPEED_LIMIT`/`TIME` on `EasyTransferOptions.LowSpeedLimit`/`LowSpeedTime`, and the engine already has the watchdog path BL-3163 fixed (`TransferPerformer.StartLowSpeedWatchdog` -> `LowSpeedWatchdog.StartFromCommandLine`, wired by `TransferContextFactory.WatchedOutput`/`WatchedProgress`/`TokenOf`, and mapped to exit 28 by `TransferPerformer.FollowWatchingSpeedAsync`). But `ManagedEasyTransfer.ProgressStartingWatchdogsOf` (about line 1171) returns before starting any watchdog when `toolProgress` is null, so a library handle never gets one. The fix: start the low-speed watchdog there for a library handle too (keep the `-m` one as it is unless measured otherwise). `TransferTimers.cs` is not the seam: it only gives the `[TIMER]` trace lines.
- Added `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs` to touches, since that method is where the fix lands. BL-3195 (in Doing on origin/work/dark-factory) holds that file, so the task went back to Backlog until they no longer overlap. No code was changed.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: e1d22e45 7a673b40 f5b1d08b 42f0e3f4.
- 2026-10-22: Doing -> Backlog. Needs Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs (ProgressStartingWatchdogsOf), which BL-3195 in Doing holds
- 2026-10-22: Priority Normal -> High and a multi-handle criterion added, from Stewart's prioritised findings list (P1, reports 3 and 25: "easy, multi and CLI").
