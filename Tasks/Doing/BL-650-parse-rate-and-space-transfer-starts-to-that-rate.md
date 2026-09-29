---
id: BL-650
title: Parse --rate and space transfer starts to that rate
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-650 — Parse --rate and space transfer starts to that rate

## Goal

`--rate <N/unit>` (`s`, `m`, `h`, `d`, with an optional multiplier such as `2/3s`) parses with curl 8.21.0's checks and makes the runner wait between transfer starts so no more than that many start per period, as curl does for serial transfers.

## Context

- Conformance audit 2026-09-28, row 30 (Major).
- The per-URL loop is `Curl.Console/CurlCommandRunner.cs`; wait on the injected `TimeProvider`, never `Thread.Sleep`. Whether the wait counts from the previous start or its end, and whether `-Z` ignores `--rate`, must be measured.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Connections 3`: `--rate 2/s` and `--rate 1/3s` with three URLs (the script reports curl's run time), and `--rate 0`, `--rate 1/x`, `--rate abc`; timings, stderr and exit codes copied into Notes.
- [ ] `Curl.Cli.UnitTests` pin parsing and refusals; `Curl.Console.UnitTests` on a fake `TimeProvider` pin when each transfer starts.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
