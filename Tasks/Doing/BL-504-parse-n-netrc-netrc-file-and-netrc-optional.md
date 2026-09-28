---
id: BL-504
title: Parse -n/--netrc, --netrc-file and --netrc-optional
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-504 — Parse -n/--netrc, --netrc-file and --netrc-optional

## Goal

`-n`/`--netrc`, `--netrc-file <path>` and `--netrc-optional` parse into `CommandLineOptions` (whether netrc is required, optional or off, and the file named), with their `--no-` forms as the alias table allows, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 6 (Blocker). Reading the file is BL-503; applying it is BL-505.
- Alias-table rows: `netrc` (`n`, `--no-` accepted), `netrc-file` (no `--no-`), `netrc-optional` (`--no-` accepted) in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`.
- The manual (`CurlManual.txt`) says `--netrc-file` implies `--netrc` and cannot be combined with `--netrc-optional`; how the combination is refused (or not) must be measured.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `-n --netrc-optional`, `--netrc-file f --netrc-optional`, `--netrc-file f -n`, `--no-netrc`; stderr and exit code copied into Notes.
- [ ] Every spelling and measured combination is covered by `Curl.Cli.UnitTests` data rows.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
