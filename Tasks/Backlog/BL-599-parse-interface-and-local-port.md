---
id: BL-599
title: Parse --interface and --local-port
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-599 — Parse --interface and --local-port

## Goal

`--interface <name>` (with curl's `if!`, `host!` and `ifhost!` prefixes) and `--local-port <num>[-num]` parse into `CommandLineOptions` with curl 8.21.0's range checks and refusals, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 13 (Major). Binding is BL-600.
- Rows `interface` and `local-port` in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; text in `CurlManual.txt`.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--local-port 0`, `--local-port 70000`, `--local-port 5-3`, `--local-port abc`, `--interface ""`; stderr and exit code copied into Notes.
- [ ] Every form and measured refusal is covered by `Curl.Cli.UnitTests` data rows.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
