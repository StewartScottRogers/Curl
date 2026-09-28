---
id: BL-607
title: Parse --pinnedpubkey, --crlfile, --cert-status and --ssl-auto-client-cert
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-607 — Parse --pinnedpubkey, --crlfile, --cert-status and --ssl-auto-client-cert

## Goal

The four options parse into `CommandLineOptions` (`--pinnedpubkey` as a file path or `sha256//` hash list, kept verbatim for the connector to check), instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 17 (Major; exits 82, 83, 90, 91 never produced). Applying them: BL-608 to BL-610.
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; texts in `CurlManual.txt`.

## Acceptance criteria

- [ ] Every option and `--no-` form the alias table allows is covered by `Curl.Cli.UnitTests`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
