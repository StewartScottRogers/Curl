---
id: BL-506
title: Parse --unix-socket and --abstract-unix-socket
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-506 — Parse --unix-socket and --abstract-unix-socket

## Goal

`--unix-socket <path>` and `--abstract-unix-socket <path>` parse into `CommandLineOptions` (the path, and whether it is abstract; the later option wins), instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 7 (Blocker). Dialling is BL-507.
- Alias-table rows `unix-socket` and `abstract-unix-socket` (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`), no `--no-` form.
- Whether the Windows reference build accepts `--abstract-unix-socket` at parse time or refuses it later must be measured.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--abstract-unix-socket x http://h/` and `--unix-socket "" http://h/` on the reference build; stderr and exit code copied into Notes.
- [ ] Both options and their interplay are covered by `Curl.Cli.UnitTests` data rows; a measured parse-time refusal is pinned.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
