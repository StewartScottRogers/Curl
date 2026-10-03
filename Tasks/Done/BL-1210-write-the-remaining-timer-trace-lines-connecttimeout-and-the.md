---
id: BL-1210
title: Write the remaining [TIMER] trace lines: CONNECTTIMEOUT and the multi's expires-in lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1186]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1210 — Write the remaining [TIMER] trace lines: CONNECTTIMEOUT and the multi's expires-in lines

## Goal

Curl writes curl 8.21.0's `[TIMER] [CONNECTTIMEOUT]` lines, and the `[TIMER] ... expires in` lines that appear only when the multi is traced, under `--trace-config timer`, `network`, `all` and `-vvvv`.

## Context

- Split from BL-1186, which writes the `[TIMER] [HAPPY_EYEBALLS]` `set for`, `gives multi timeout in` and `cleared` lines from `ConnectAttemptTraceEvents`.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `curl -s --connect-timeout 1 http://localhost:1/`:
  - `-v --trace-config timer,multi`: `[TIMER] [CONNECTTIMEOUT] set for 1000000ns` first (`[0-x]` under `--trace-ids`), then `[HAPPY_EYEBALLS] set for 200000ns`, `[HAPPY_EYEBALLS] expires in 199989ns`, `[CONNECTTIMEOUT] expires in 999499ns`, `[HAPPY_EYEBALLS] gives multi timeout in 201ms`, later `[CONNECTTIMEOUT] expires in 792715ns` and `[CONNECTTIMEOUT] gives multi timeout in 793ms`.
  - `-vv --trace-config timer` and `-v --trace-config timer,tcp`: no `expires in` lines; `set` and `gives multi timeout` only. So `expires in` needs `multi` (which `network`, `all` and `-vvvv` include).
  - Values are volatile (ns elapsed, ms rounded up); decide how to write them under ADR-0357.
- Without `--connect-timeout` a plain 127.0.0.1 connect writes no CONNECTTIMEOUT line under `timer` (BL-1186 measurement); check `-m` and the default 300 s connect timeout before pinning.

## Acceptance criteria

- [x] Tests pin the `[TIMER] [CONNECTTIMEOUT]` lines of a `--connect-timeout` connect and the `expires in` lines under `network`, and that `expires in` is absent under `timer` alone.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`, fixtures in `%TEMP%\bl1210`, `http://127.0.0.1:P/` unless stated:
  - `-v --trace-config timer --connect-timeout 1`: `[TIMER] [CONNECTTIMEOUT] set for 1000000ns`, `Trying`, `[CONNECTTIMEOUT] gives multi timeout in 1000ms`, `[HAPPY_EYEBALLS] cleared`, `Established`; then once more `gives` after `Request completely sent off`.
  - Under `network`: `set` comes before the `[DNS] created` line; `[CONNECTTIMEOUT] expires in 999129ns` then `gives ... 1000ms` after the first poll round.
  - `localhost` (two families) under `network`: `[HAPPY_EYEBALLS] set for 200000ns`, poll round, `[HAPPY_EYEBALLS] expires in`, `[CONNECTTIMEOUT] expires in`, `[HAPPY_EYEBALLS] gives multi timeout in 201ms`; after the IPv4 attempt opens, `[CONNECTTIMEOUT] expires in 797637ns` and `gives ... 798ms`.
  - No `--connect-timeout`, or `--connect-timeout 0`: no `CONNECTTIMEOUT` line under `timer` or `network`.
  - `-m 5`: `[TIMER] [TIMEOUT]` lines instead; `-m 5 --connect-timeout 1` writes both `set` lines and CONNECTTIMEOUT `gives`; `-m 1 --connect-timeout 5` names `[TIMEOUT]` in `gives`. Filed as BL-1258.
- Delivered: `ConnectAttemptTraceEvents` takes `tracesTimerExpiry` and `connectTimeout`; `ConnectStarting` writes `set` (called by `TcpConnector.TracingConnectionFilters` before the `[SETUP]`/`[DNS]` filters' first lines); after the first attempt it writes every pending timer's `expires in` (under `multi`) and the nearest timer's `gives`; after a second family started on time, the connect timeout's again less the delay. `TcpConnector.TracesHappyEyeballsTimer` renamed `TracesTimers` (it now covers both timers); new `TracesTimerExpiry` (`CurlComposition.TracesMulti`) and `TracedConnectTimeout` (`--connect-timeout` when positive; the connector's own limit defaults to 300 s and takes `-m`, so it cannot be used).
- Decisions (ADR-0390): values from the configured delays (`1000000ns`, `1000ms`, `800000ns`/`800ms` after a 200 ms second family), milliseconds rounded up; lines follow the attempt's socket lines with no poll round in between (BL-1186's choice); the response-wait lines and `-m`'s `[TIMEOUT]` left to BL-1258.
- Tests: `ConnectAttemptTraceEventsTests.ConnectTimeoutOnAPlainConnect_*`, `TwoFamiliesWithAConnectTimeoutUnderTheMulti_*`, `AConnectTimeoutShorterThanTheSecondFamilysDelay_*`, `ASecondFamilyStartedAfterTheFirstFailed_*`, `WithoutAConnectTimeoutOrTheTimer_*`; `TcpConnectorTests.ConnectAsync_TracingTimersWithAConnectTimeoutAcrossTwoFamilies_*`; `CurlCompositionDnsTraceTests.Connect_UnderTraceConfigTimerWithAConnectTimeout_*` (no `expires in` under `timer`), `Connect_WithTheTimerAndMultiComponentsAndAConnectTimeout_*` (`network`, `timer,multi`, `all`, `-vvvv`), `Connect_WithoutAConnectTimeout_*`.
- `Measure-CodeQuality.ps1`: Curl.Networking.UnitLibrary and Curl.Console 100% line and branch, 0 failing members.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config timer and --connect-timeout write curl's [TIMER] [CONNECTTIMEOUT] set and gives lines, and multi adds each timer's expires-in line; -m and the response wait split to BL-1258
