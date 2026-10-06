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
completed: 2026-09-28
---
# BL-607 — Parse --pinnedpubkey, --crlfile, --cert-status and --ssl-auto-client-cert

## Goal

The four options parse into `CommandLineOptions` (`--pinnedpubkey` as a file path or `sha256//` hash list, kept verbatim for the connector to check), instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 17 (Major; exits 82, 83, 90, 91 never produced). Applying them: BL-608 to BL-610.
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; texts in `CurlManual.txt`.

## Acceptance criteria

- [x] Every option and `--no-` form the alias table allows is covered by `Curl.Cli.UnitTests`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured the local curl 8.21.0 (Schannel) on 2026-09-28: `--crlfile` checks the file exists exactly as `--cacert` does (same two refusal lines, flag-like warning first), so it reuses `SettingExistingFile`; `--pinnedpubkey` is plain text (blank refused, no flag-like warning); `--cert-status` and `--ssl-auto-client-cert` are negatable flags. All four are per-group options, as in curl's `OperationConfig`.
- Property names: `CertificateRevocationListFile`, `PinnedPublicKey`, `RequireCertificateStatus`, `AutoClientCertificate`. Parsing only; BL-608 to BL-610 apply them. No ADR: no design choice beyond matching measured curl.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --pinnedpubkey, --crlfile, --cert-status and --ssl-auto-client-cert parse into CommandLineOptions as curl 8.21.0 does
