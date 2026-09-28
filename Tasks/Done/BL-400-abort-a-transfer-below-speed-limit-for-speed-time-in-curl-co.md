---
id: BL-400
title: Abort a transfer below --speed-limit for --speed-time in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-196]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-400 — Abort a transfer below --speed-limit for --speed-time in Curl.Console

## Goal

A transfer whose speed stays below `-Y`/`--speed-limit` bytes per second for `-y`/`--speed-time` seconds ends with exit 28 and curl 8.21.0's message.

## Context

- Filed by BL-241 (2026-09-27): BL-196 parses `-Y` and `-y` into `CommandLineOptions`, but nothing acts on them. BL-241 wired `--limit-rate` as a `RateLimitedStream` around each attempt's output on the runner's clock; a low-speed watchdog can wrap the same output (or read `TransferProgressRecorder`).
- Upstream: https://curl.se/docs/manpage.html (`-Y`, `-y`; `-y` defaults to 30 seconds when only `-Y` is given). Measure curl 8.21.0 with `Record-CurlExchange.ps1` (a slow canned response) before pinning the message text.

## Acceptance criteria

- [x] `-Y 100 -y 2` against a fake handler that stalls ends with exit 28 and the measured `curl: (28) ...` line, on an injected `TimeProvider`.
- [x] `-Y` without `-y` uses curl's 30-second default; `-y` without `-Y` sets no limit, as measured. (Measured: it watches for 1 byte per second, not no limit; see Notes.)
- [x] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no failing member for the projects touched.

## Notes

- Measured 2026-09-27, curl 8.21.0 (Windows, Schannel), `Record-CurlExchange.ps1 -ResponseDelayMilliseconds` (server holds the response back), all exit 28:
  - `-sS -Y 100 -y 2`: `curl: (28) Operation too slow. Less than 100 bytes/sec transferred the last 2 seconds`
  - `-sS -Y 100`: `... Less than 100 bytes/sec transferred the last 30 seconds`
  - `-sS -y 2`: `... Less than 1 bytes/sec transferred the last 2 seconds`
  - `-sS -Y 0 -y 2`: `... Less than 1 bytes/sec transferred the last 2 seconds`
- The second criterion's "sets no limit" contradicts the measurement (and BL-196's `--libcurl` reading); "as measured" governs, so `-y` alone watches for 1 byte per second.
- Design (ADR-0105): `Curl.Core`'s `LowSpeedWatchdog` samples once a second on the runner's clock, measures the faster of output bytes and reported upload bytes over the last six samples as `Curl_speedcheck` does, and cancels its token after a slow spell lasting the speed time. `TransferContextFactory` wraps the output and progress sink and passes the token as the context's `CancellationToken`; `CurlCommandRunner.FollowWatchingSpeedAsync` turns the resulting `OperationCanceledException` into exit 28. Works for every handler that honours the context's token, with no protocol change.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0105 and its index row; no task in Doing (BL-449, BL-452) names it.
- Order-dependent zero edge (`-Y 0 -y 2`) filed as BL-467.
- `Measure-CodeQuality.ps1`: Curl.Core.UnitLibrary 100/100, 0 failing. Curl.Console's only two failing members are pre-existing and already filed: `DiskWriteOutFileOpener.TryOpen` (BL-432) and `DumpHeaderOutputStream.WriteAsync` (BL-455, BL-462); every member this task added or changed is at 100%.
- Tests: `LowSpeedWatchdogTests` (19, Core), `CurlCommandRunnerSpeedLimitTests` (8, Console).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -Y/-y abort a transfer that stays too slow with exit 28 and curl 8.21.0's message
