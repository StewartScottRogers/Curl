---
id: BL-522
title: Parse --proto, --proto-redir and --proto-default into scheme sets
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-522 — Parse --proto, --proto-redir and --proto-default into scheme sets

## Goal

`--proto <protocols>` and `--proto-redir <protocols>` are read with curl 8.21.0's syntax (comma-separated names, `+` add, `-` remove, `=` set, `all`, applied left to right) into the set of allowed schemes, and `--proto-default <protocol>` into one scheme, with curl's warnings and refusals for unknown names.

## Context

- Conformance audit 2026-09-28, row 10 (Blocker). Enforcing them is BL-523 and BL-524.
- Alias-table rows `proto`, `proto-redir`, `proto-default` (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`). The syntax is in `CurlManual.txt` (`--proto`) and https://curl.se/docs/manpage.html#--proto (8.21.0 reference).
- The scheme names are those curl 8.21.0 knows, including ones Curl does not implement; the unknown-name warning text and whether an unknown `--proto-default` is refused must be measured.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--proto =http,https`, `--proto -all,+http`, `--proto http,bogus`, `--proto ""`, `--proto-default bogus`, each with a loopback `http://` URL; stderr and exit code copied into Notes.
- [ ] `Curl.Cli.UnitTests` data rows cover each syntax form, order of application, `all`, and the measured warning and refusal texts byte for byte.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
