---
id: BL-470
title: Apply -Y and -y defaults in command-line order so -Y 0 -y 2 watches 1 byte per second
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-400]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-470 — Apply -Y and -y defaults in command-line order so -Y 0 -y 2 watches 1 byte per second

## Goal

`-Y 0 -y 2` aborts a stalled transfer after 2 seconds below 1 byte per second, as curl 8.21.0 does, while `-y 2 -Y 0` and `-Y 100 -y 0` still watch nothing.

## Context

- Filed by BL-400 (ADR-0106, Consequences). curl's tool sets a zero `low_speed_limit` to 1 when `-y` is parsed and a zero `low_speed_time` to 30 when `-Y` is parsed, so the defaults depend on the order the options came in. `CommandLineOptions` keeps only the last value of each, and `LowSpeedWatchdog.StartFromCommandLine` applies the defaults to `null` only, so `-Y 0 -y 2` watches nothing.
- Measured by BL-400 on curl 8.21.0 (Windows): `-sS -Y 0 -y 2` against a stalled server prints `curl: (28) Operation too slow. Less than 1 bytes/sec transferred the last 2 seconds` and exits 28. Measure `-y 2 -Y 0` and `-Y 100 -y 0` with `Record-CurlExchange.ps1` (`-ResponseDelayMilliseconds`) before pinning them.

## Acceptance criteria

- [ ] `-Y 0 -y 2` against a stalling handler ends with exit 28 and the measured line, on an injected `TimeProvider`.
- [ ] `-y 2 -Y 0` and `-Y 100 -y 0` behave as measured.
- [ ] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no new failing member for the projects touched.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
