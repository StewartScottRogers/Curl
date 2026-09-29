---
id: BL-645
title: Parse --keepalive-time and --keepalive-cnt and set the TCP keepalive timers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-490]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-645 — Parse --keepalive-time and --keepalive-cnt and set the TCP keepalive timers

## Goal

`--keepalive-time <seconds>` (idle and interval, default 60) and `--keepalive-cnt <n>` (probe count) parse with curl 8.21.0's checks and set `TcpKeepAliveTime`, `TcpKeepAliveInterval` and `TcpKeepAliveRetryCount` on the socket where the platform supports them, alongside the SO_KEEPALIVE switch BL-490 wires.

## Context

- Conformance audit 2026-09-28, row 29 (Major, S).
- Socket options go through the `ITcpDialer` seam (ADR-0083); the BCL's `SocketOptionName.TcpKeepAliveTime`/`Interval`/`RetryCount` exist, with platform limits (the retry count is not settable on every Windows version): pin each platform's behaviour.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--keepalive-time 0`, `--keepalive-time -1`, `--keepalive-cnt 0`, `--keepalive-cnt abc`; stderr and exit code copied into Notes.
- [ ] `Curl.Cli.UnitTests` pin parsing and refusals; `Curl.Networking.UnitTests` pin the socket options requested through the dialer seam, and none with `--no-keepalive`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
