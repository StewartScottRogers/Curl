---
id: BL-517
title: Parse -Z/--parallel, --parallel-max, --parallel-immediate and --parallel-max-host
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-508]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-517 — Parse -Z/--parallel, --parallel-max, --parallel-immediate and --parallel-max-host

## Goal

`-Z`/`--parallel` (and `--no-parallel`), `--parallel-max <num>`, `--parallel-immediate` (and `--no-` form) and `--parallel-max-host <num>` parse into `CommandLineOptions` with curl 8.21.0's defaults, ranges and refusals, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 9 (Blocker, L; split: this parse, BL-518 decision, BL-519 run, BL-520 limits, BL-521 meter).
- Alias-table rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`: `parallel` (`Z`, `--no-` accepted), `parallel-immediate` (`--no-` accepted), `parallel-max`, `parallel-max-host`. These are global options (they apply across `--next` groups), so this task waits for BL-508, which introduces the global/per-group split, and classifies all four as global.
- The manual (`CurlManual.txt`) gives `--parallel-max` default 50 and upper limit 300; what curl does with `0`, `301` and `-1` must be measured (clamped or refused).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--parallel-max 0`, `--parallel-max 301`, `--parallel-max -1`, `--parallel-max-host 0`, with `-Z` and a loopback URL; stderr and exit code copied into Notes.
- [ ] Every spelling, the defaults and each measured edge case are covered by `Curl.Cli.UnitTests` data rows.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
