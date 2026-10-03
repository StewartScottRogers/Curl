---
id: BL-1258
title: Write the [TIMER] [TIMEOUT] lines of -m and the timer lines of the response wait
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1210]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1258 — Write the [TIMER] [TIMEOUT] lines of -m and the timer lines of the response wait

## Goal

Curl writes curl 8.21.0's `[TIMER] [TIMEOUT]` lines of `-m` under `--trace-config timer`, `network`, `all` and `-vvvv`, with the nearest of `-m` and `--connect-timeout` named in `gives multi timeout`, and decides (ADR) whether the timer lines curl writes while waiting for a response are written.

## Context

- Split from BL-1210 (ADR-0390), which writes the connect phase's `[TIMER] [CONNECTTIMEOUT]` and `expires in` lines in `ConnectAttemptTraceEvents`.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `http://127.0.0.1:P/`:
  - `-v --trace-config timer -m 5`: `[TIMER] [TIMEOUT] set for 4999999ns` first, `[TIMEOUT] gives multi timeout in 5000ms` after `Trying`, again after `Request completely sent off`.
  - `-m 5 --connect-timeout 1`: `[TIMEOUT] set for 5000000ns`, `[CONNECTTIMEOUT] set for 1000000ns`, then `[CONNECTTIMEOUT] gives ... 1000ms`. `-m 1 --connect-timeout 5`: the `gives` lines name `[TIMEOUT]` with 1000ms.
  - After `Request completely sent off` curl writes the nearest timer's `gives` (and `expires in` under `multi`) only when the response has not arrived at its first poll; racy on loopback.
- `TcpConnector.TracedConnectTimeout` carries `--connect-timeout`; `-m` will need its own value passed the same way from `CurlComposition`.

## Acceptance criteria

- [ ] Tests pin the `[TIMER] [TIMEOUT]` `set` line of `-m 5` and which timer `gives multi timeout` names for `-m 5 --connect-timeout 1` and `-m 1 --connect-timeout 5`.
- [ ] An ADR records whether and when the response-wait timer lines are written.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
