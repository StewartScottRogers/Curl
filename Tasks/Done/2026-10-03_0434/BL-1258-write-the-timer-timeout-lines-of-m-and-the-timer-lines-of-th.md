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
completed: 2026-10-02
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

- [x] Tests pin the `[TIMER] [TIMEOUT]` `set` line of `-m 5` and which timer `gives multi timeout` names for `-m 5 --connect-timeout 1` and `-m 1 --connect-timeout 5`.
- [x] An ADR records whether and when the response-wait timer lines are written.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`, fixtures in `%TEMP%\bl1258`:
  - `-m 5` under `timer`: `[TIMEOUT] set for 4999999ns`, `Trying`, `[TIMEOUT] gives multi timeout in 5000ms`, `[HAPPY_EYEBALLS] cleared`, `Established`, `Request completely sent off`, `[TIMEOUT] gives ... 5000ms`.
  - `-m 5 --connect-timeout 1`: `[TIMEOUT] set for 5000000ns`, `[CONNECTTIMEOUT] set for 999999ns`, `gives` names CONNECTTIMEOUT 1000ms (both times). `-m 1 --connect-timeout 5`: both `set` lines (so a shorter `-m` does not silence `--connect-timeout`'s), `gives` names TIMEOUT 1000ms.
  - Under `network` with both: `expires in` for CONNECTTIMEOUT then TIMEOUT (nearest first), then `gives`; in the response wait they sit between `[MULTI] [PERFORMING] pollset[fd=N IN], timeouts=2` and `multi_wait(fds=1, timeout=1000) tinternal=1000`. With `-m 5` alone `timeouts=1`, `tinternal=5000`; the CONNECTING poll lines carry the same values.
  - `localhost -m 5`: after the IPv4 attempt `[TIMEOUT] gives ... 4791ms`.
  - The response-wait lines came in all seven loopback runs.
- Delivered: `ConnectAttemptTraceEvents` takes `transferTimeout`, writes both `set` lines (TIMEOUT first), orders `expires in` nearest first and subtracts a second family's delay from every given timer; `TcpConnector.TracedTransferTimeout` (from a positive `-m` in `CurlComposition`). Console: `TransferTimers` (count, `tinternal`, wait lines), `MultiStateTraceEvents` takes it for its poll lines and the response-wait lines, `ResponseWaitTimerTraceEvents` writes them after `Request completely sent off` when `timer` is traced without `multi`.
- Decisions (ADR-0401): the response-wait lines are always written (every run measured wrote them); values from the configured delays (`4800ms` where curl's clock gave `4791ms`); ties name `-m`'s timer first.
- Tests: `ConnectAttemptTraceEventsTests.MaxTimeOnAPlainConnect_*`, `MaxTimeAndAConnectTimeout_*`, `MaxTimeAcrossTwoFamilies_*`; `CurlCompositionDnsTraceTests.Connect_UnderTraceConfigTimerWithMaxTime_*`, `Connect_UnderTraceConfigTimerWithMaxTimeAndAConnectTimeout_*`, `Connect_UnderTraceConfigTimerWithMaxTimeZero_*`; `CurlCommandRunnerTransferEventTests.RunAsync_TraceConfigTimerWithTimeouts_*`, `RunAsync_TraceConfigNetworkWithMaxTimeAndAConnectTimeout_*`, `RunAsync_TraceConfigMultiWithMaxTimeButNotTimer_*`, `RunAsync_TraceConfigTimerWithoutMaxTimeOrConnectTimeout_*`; `TransferTimersTests`, `ResponseWaitTimerTraceEventsTests`.
- `Measure-CodeQuality.ps1`: Curl.Networking.UnitLibrary and Curl.Console 100% line and branch, 0 failing members. No option changed, so `--ai-help` is untouched.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config timer writes -m's [TIMER] [TIMEOUT] lines beside the connect timeout's, the nearest named in gives, and the response wait's timer lines (ADR-0401)
