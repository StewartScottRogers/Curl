---
id: BL-634
title: Parse --ftp-account, --ftp-alternative-to-user, --ftp-pret, --ftp-ssl-ccc and --ftp-ssl-ccc-mode
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-634 — Parse --ftp-account, --ftp-alternative-to-user, --ftp-pret, --ftp-ssl-ccc and --ftp-ssl-ccc-mode

## Goal

The five FTP options parse into `CommandLineOptions` with curl 8.21.0's value checks (`--ftp-ssl-ccc-mode active|passive`), instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 25 (Major). Behaviour: BL-635 (ACCT, alternative user, PRET) and BL-636 (CCC).
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; the other FTP options (`--ftp-method`, `--ftp-pasv`, `--disable-epsv`) are rows in `CommandLineOptionTable.cs` to follow.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Ftp`: `--ftp-ssl-ccc-mode bogus`; stderr and exit code copied into Notes.
- [ ] Every option and `--no-` form is covered by `Curl.Cli.UnitTests`, with the measured refusal.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
