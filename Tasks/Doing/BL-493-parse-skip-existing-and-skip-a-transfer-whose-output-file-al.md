---
id: BL-493
title: Parse --skip-existing and skip a transfer whose output file already exists
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-493 — Parse --skip-existing and skip a transfer whose output file already exists

## Goal

With `--skip-existing`, a transfer whose `-o`/`-O` file already exists is not performed at all (no connection is made) and the run carries on to the next URL, printing and exiting as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 11 (Blocker, S).
- Alias-table row `skip-existing` (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`); no row in `CommandLineOptionTable.cs`.
- The per-URL loop is in `Curl.Console/CurlCommandRunner.cs`; output files go through `IOutputPaths`.
- The manual (`CurlManual.txt`, `--skip-existing`) says the transfer is skipped when the file exists; the note curl prints (and whether only under `-v`) must be measured.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (`-o out.txt --skip-existing`, with and without `-v`, file present and absent, and two URLs where only the first file exists): stdout, stderr, exit code and whether a connection was accepted, copied into Notes.
- [ ] `--skip-existing` parses; `Curl.Cli.UnitTests` covers it.
- [ ] A `Curl.Console.UnitTests` test shows no handler call and no connection for an existing file, the measured stderr and exit code, and the second URL still transferred.
- [ ] New tests are platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
