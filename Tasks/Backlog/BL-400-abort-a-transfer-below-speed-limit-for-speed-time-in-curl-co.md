---
id: BL-400
title: Abort a transfer below --speed-limit for --speed-time in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-196]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-400 — Abort a transfer below --speed-limit for --speed-time in Curl.Console

## Goal

A transfer whose speed stays below `-Y`/`--speed-limit` bytes per second for `-y`/`--speed-time` seconds ends with exit 28 and curl 8.21.0's message.

## Context

- Filed by BL-241 (2026-09-27): BL-196 parses `-Y` and `-y` into `CommandLineOptions`, but nothing acts on them. BL-241 wired `--limit-rate` as a `RateLimitedStream` around each attempt's output on the runner's clock; a low-speed watchdog can wrap the same output (or read `TransferProgressRecorder`).
- Upstream: https://curl.se/docs/manpage.html (`-Y`, `-y`; `-y` defaults to 30 seconds when only `-Y` is given). Measure curl 8.21.0 with `Record-CurlExchange.ps1` (a slow canned response) before pinning the message text.

## Acceptance criteria

- [ ] `-Y 100 -y 2` against a fake handler that stalls ends with exit 28 and the measured `curl: (28) ...` line, on an injected `TimeProvider`.
- [ ] `-Y` without `-y` uses curl's 30-second default; `-y` without `-Y` sets no limit, as measured.
- [ ] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no failing member for the projects touched.

## Notes

## Log

- 2026-09-27: Created.
