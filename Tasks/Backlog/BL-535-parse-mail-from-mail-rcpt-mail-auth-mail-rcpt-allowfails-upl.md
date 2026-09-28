---
id: BL-535
title: Parse --mail-from, --mail-rcpt, --mail-auth, --mail-rcpt-allowfails, --upload-flags, --sasl-authzid, --sasl-ir and --login-options
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-533]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-535 — Parse --mail-from, --mail-rcpt, --mail-auth, --mail-rcpt-allowfails, --upload-flags, --sasl-authzid, --sasl-ir and --login-options

## Goal

The eight mail and SASL options parse into `CommandLineOptions` as curl 8.21.0 reads them (`--mail-rcpt` repeatable and kept in order, `--sasl-ir` and `--mail-rcpt-allowfails` with their `--no-` forms, `--upload-flags` with curl's flag syntax), instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, rows 31 and 23. The ADR from BL-533 names where each value ends up.
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; texts in `CurlManual.txt`. `--upload-flags` takes a comma list of IMAP flags (`answered`, `deleted`, `draft`, `flagged`, `seen`, with `-` to unset); its refusal for an unknown flag must be measured.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--upload-flags bogus`, `--upload-flags seen,-draft`, `--mail-rcpt ""`, `--login-options ""` with a loopback URL; stderr and exit code copied into Notes.
- [ ] Every option and `--no-` form is covered by `Curl.Cli.UnitTests` data rows, including repeated `--mail-rcpt` order and the measured refusals byte for byte.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
