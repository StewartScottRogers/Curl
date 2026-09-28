---
id: BL-501
title: Parse -1/--tlsv1, --tlsv1.0, --tlsv1.1, --tls-max and --proxy-tlsv1
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-501 — Parse -1/--tlsv1, --tlsv1.0, --tlsv1.1, --tls-max and --proxy-tlsv1

## Goal

`-1`/`--tlsv1`, `--tlsv1.0`, `--tlsv1.1` set the minimum TLS version as `--tlsv1.2` and `--tlsv1.3` already do, `--tls-max <VERSION>` sets a maximum (`1.0`, `1.1`, `1.2`, `1.3`, `default`), and `--proxy-tlsv1` sets the HTTPS proxy's minimum, with curl 8.21.0's refusal for a bad `--tls-max` value.

## Context

- Conformance audit 2026-09-28, row 5 (Blocker).
- `--tlsv1.2`/`--tlsv1.3` are rows in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` setting `MinimumTlsVersion` (`SslProtocols`); follow them. `SslProtocols.Tls` and `Tls11` are obsolete in .NET (SYSLIB0039) and warnings are errors, so choose a representation that does not trip the analyzer (for instance a Curl-owned enum, as `Curl.Networking.UnitLibrary/TlsMinimumVersion.cs` suggests).
- Using the values is BL-502.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--tls-max 1.4`, `--tls-max abc` and `--tls-max` with no value; stderr and exit code copied into Notes.
- [ ] Every spelling (including `-1` in a bundle) is covered by `Curl.Cli.UnitTests` data rows; the measured refusals are pinned byte for byte.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean (no SYSLIB0039 suppression outside one documented place), the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
