---
id: BL-630
title: Parse --krb, --delegation, --service-name and --proxy-service-name
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-630 — Parse --krb, --delegation, --service-name and --proxy-service-name

## Goal

`--krb <level>`, `--delegation <none|policy|always>`, `--service-name <name>` and `--proxy-service-name <name>` parse into `CommandLineOptions` with curl 8.21.0's value checks, and `--krb` (FTP Kerberos) is accepted or refused exactly as the platform's reference build does.

## Context

- Conformance audit 2026-09-28, row 23 (Major). `--sasl-authzid`, `--sasl-ir` and `--login-options` are BL-535. Applying the names is BL-631.
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; texts in `CurlManual.txt`. FTP Kerberos (`--krb`) is often not built in; if the reference build refuses it, pin that refusal and do not build FTP Kerberos.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Ftp`: `--krb private ftp://...`, `--krb bogus`, `--delegation bogus`; stderr and exit code copied into Notes, on Windows and on Linux or macOS.
- [ ] `Curl.Cli.UnitTests` cover every option and measured refusal, per platform with `OSCondition` where they differ.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
