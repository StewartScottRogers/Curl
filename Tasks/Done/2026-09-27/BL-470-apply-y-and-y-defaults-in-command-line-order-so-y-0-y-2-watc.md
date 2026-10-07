---
id: BL-470
title: Apply -Y and -y defaults in command-line order so -Y 0 -y 2 watches 1 byte per second
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-400]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-470 — Apply -Y and -y defaults in command-line order so -Y 0 -y 2 watches 1 byte per second

## Goal

`-Y 0 -y 2` aborts a stalled transfer after 2 seconds below 1 byte per second, as curl 8.21.0 does, while `-y 2 -Y 0` and `-Y 100 -y 0` still watch nothing.

## Context

- Filed by BL-400 (ADR-0106, Consequences). curl's tool sets a zero `low_speed_limit` to 1 when `-y` is parsed and a zero `low_speed_time` to 30 when `-Y` is parsed, so the defaults depend on the order the options came in. `CommandLineOptions` keeps only the last value of each, and `LowSpeedWatchdog.StartFromCommandLine` applies the defaults to `null` only, so `-Y 0 -y 2` watches nothing.
- Measured by BL-400 on curl 8.21.0 (Windows): `-sS -Y 0 -y 2` against a stalled server prints `curl: (28) Operation too slow. Less than 1 bytes/sec transferred the last 2 seconds` and exits 28. Measure `-y 2 -Y 0` and `-Y 100 -y 0` with `Record-CurlExchange.ps1` (`-ResponseDelayMilliseconds`) before pinning them.

## Acceptance criteria

- [x] `-Y 0 -y 2` against a stalling handler ends with exit 28 and the measured line, on an injected `TimeProvider`.
- [x] `-y 2 -Y 0` and `-Y 100 -y 0` behave as measured.
- [x] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no new failing member for the projects touched.

## Notes

- Measured 2026-09-27, curl 8.21.0 (Windows, Schannel), `Record-CurlExchange.ps1` with a head
  promising 100 bytes, 3 sent, connection held 6 s (`-HoldOpenMilliseconds 6000`):
  `-sS -Y 0 -y 2` exit 28 `Operation too slow. Less than 1 bytes/sec transferred the last 2 seconds`;
  `-y 2 -Y 0`, `-Y 100 -y 0` and `-y 0 -Y 5` exit 18 `end of response with 97 bytes missing`
  (nothing watched, or a 30 s watch outlasting the server).
- Decision (ADR-0115, decided by Claude under Stewart's delegation): the `-Y`/`-y` appliers in
  `CommandLineOptionTable` apply curl's tool defaults as they parse (`-Y` turns a zero speed
  time into 30, `-y` a zero limit into 1); a not-yet-given option stays `null`, so
  `LowSpeedWatchdog` in `Curl.Core` is unchanged. Only the Cli library changed in production;
  `Curl.Console` gained tests only.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0115 and its index row; no
  task in `Doing` names it.
- Tests: `CommandLineRetryAndSpeedOptionTests.Parse_SpeedOptionsAfterAZeroOther_ApplyCurlsDefaultsInCommandLineOrder`
  (6 rows), `CurlCommandRunnerSpeedLimitTests.RunAsync_SpeedTimeAfterZeroSpeedLimit_WatchesForOneBytePerSecond`,
  `RunAsync_ZeroLastSpeedOption_WatchesNothing` (2 rows). Cli 2070 passed, Console 988 passed;
  `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line and branch, 0 failing.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -Y and -y apply curl's defaults in command-line order: -Y 0 -y 2 aborts after 2 s below 1 byte/s, -y 2 -Y 0 and -Y 100 -y 0 watch nothing
