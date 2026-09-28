---
id: BL-562
title: Parse --pubkey, --knownhosts, --hostpubmd5, --hostpubsha256 and --compressed-ssh
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-560]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-562 — Parse --pubkey, --knownhosts, --hostpubmd5, --hostpubsha256 and --compressed-ssh

## Goal

The five SSH options parse into `CommandLineOptions` as curl 8.21.0 reads them, including its checks on `--hostpubmd5` (32 hex digits) and `--hostpubsha256` (base64), instead of `is unknown` and exit 2; `--key`, `--key-type` and `--pass` are confirmed to reach the same options object for SSH use.

## Context

- Conformance audit 2026-09-28, row 31. Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs` (`compressed-ssh` accepts `--no-`); texts in `CurlManual.txt`.
- Measure the refusals: `--hostpubmd5 abc`, `--hostpubmd5` with 32 non-hex characters, `--hostpubsha256 !!!`, each with `sftp://127.0.0.1/x`.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -NoServer` (BL-528; the refusals happen before any connection): the cases above, stderr and exit code copied into Notes.
- [ ] Every option and `--no-compressed-ssh` is covered by `Curl.Cli.UnitTests` data rows, with the measured refusals byte for byte.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
