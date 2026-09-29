---
id: BL-637
title: Honour -z and -R for FTP downloads through MDTM
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-637 — Honour -z and -R for FTP downloads through MDTM

## Goal

An FTP download with `-z <date>` sends `MDTM` and skips the transfer when the condition is unmet (a success with no body, as `TransferResult.TimeConditionNotMet` does for `file://`), and with `-R` reports the `MDTM` time on `TransferResult.SourceLastWriteTimeUtc` so `Curl.Console` stamps the output file, both as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 26 (Major): FTP ignores `-z` and `-R`.
- `ITransferContext.TimeCondition` and `TransferResult.SourceLastWriteTimeUtc` already exist (FR-009, FR-011 for `file://`); `Curl.Console` already applies `-R` through `IFileTimeSetter`. Only the FTP handler changes. Dates parse with `CurlDateParser` (ADR-0074) where needed; `MDTM` replies are `213 YYYYMMDDHHMMSS`.
- `Record-CurlExchange.ps1 -Ftp` answers `MDTM 213 20260927123456` by default.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Ftp`: `-z` older and newer than the MDTM time, `-z -<date>`, `-R -o out`, and `MDTM` answered `550`; commands, stdout, stderr, exit code and the output file's time copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin commands and results (including `TimeConditionNotMet` and the reported time) for each case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
