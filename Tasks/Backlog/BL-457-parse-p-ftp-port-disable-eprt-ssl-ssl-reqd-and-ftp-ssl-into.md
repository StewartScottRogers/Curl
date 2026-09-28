---
id: BL-457
title: Parse -P/--ftp-port, --disable-eprt, --ssl, --ssl-reqd and --ftp-ssl* into CommandLineOptions
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-459]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-457 — Parse -P/--ftp-port, --disable-eprt, --ssl, --ssl-reqd and --ftp-ssl* into CommandLineOptions

## Goal

`-P`/`--ftp-port <address>`, `--disable-eprt` (and `--eprt`), `--ssl`, `--ftp-ssl`, `--ssl-reqd`, `--ftp-ssl-reqd` and `--ftp-ssl-control` parse into `CommandLineOptions` properties matching ADR-0102's transfer options, as curl 8.21.0 parses them.

## Context

- ADR-0102, "Contract additions", item 3. `TransportSecurityLevel` comes from BL-459.
- Today these names exist only in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; `CommandLineOptionTable.cs` has no entry. `--disable-epsv` and `--ftp-method` (BL-436) show how FTP options were added.
- curl: `--ftp-ssl` is the old name of `--ssl`, `--ftp-ssl-reqd` of `--ssl-reqd`; `--ftp-ssl-control` requires TLS for the control connection only; `--no-ssl` and `--no-ssl-reqd` turn them off. Measure how curl 8.21.0 combines them (for example `--ssl-reqd --no-ssl`) before pinning, and record the answers under Notes.

## Acceptance criteria

- [ ] `CommandLineOptions` carries `FtpPort`, `FtpUseEprt`, `SslLevel` and `FtpSslControlOnly`, with the defaults of ADR-0102; each option and its `--no-` form is pinned by a named test in `Curl.Cli.UnitTests`.
- [ ] `-P` without a value fails as curl 8.21.0 fails (measured exit code and message pinned in a named test).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for `Curl.Cli.UnitLibrary`.

## Notes

Filed by BL-437 under ADR-0102. BL-458 depends on this task.

## Log

- 2026-09-27: Created.
