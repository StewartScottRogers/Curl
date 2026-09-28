---
id: BL-644
title: Parse --happy-eyeballs-timeout-ms and race address families after that delay
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-644 — Parse --happy-eyeballs-timeout-ms and race address families after that delay

## Goal

`--happy-eyeballs-timeout-ms <ms>` parses (default 200) and the TCP connector starts the second address family's attempt that long after the first, as curl 8.21.0 does, where today it tries addresses in turn.

## Context

- Conformance audit 2026-09-28, row 28 (Minor).
- Code: `Curl.Networking.UnitLibrary/TcpConnector.cs` (dial order), with `TimeProvider` for the delay; `-4`/`-6` (BL-500) restrict the families. Measure what `-v` prints for the racing attempts and which address `%{remote_ip}` reports when both succeed.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` listening on one family only (`-ListenAddress`), against `localhost` with `-v` and `--happy-eyeballs-timeout-ms 50` and `5000`; stderr and timing copied into Notes.
- [ ] `Curl.Networking.UnitTests` on a fake `TimeProvider` pin when each family's dial starts and which connection wins; `Curl.Cli.UnitTests` pin parsing and refusals.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
