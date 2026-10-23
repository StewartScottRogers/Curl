---
id: BL-3196
title: Abort a slow API transfer with CURLOPT_LOW_SPEED_LIMIT and LOW_SPEED_TIME
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferTimers.cs, Curl.Engine.UnitTests/LowSpeedLimitTests.cs, Libcurl.UnitTests/Options/LowSpeedOptionTests.cs]
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
- [ ] BL-3163's CLI tests still pass. Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: e1d22e45 7a673b40 f5b1d08b 42f0e3f4.
