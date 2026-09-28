---
id: BL-638
title: Honour --max-filesize for FTP downloads through SIZE
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-638 — Honour --max-filesize for FTP downloads through SIZE

## Goal

An FTP download larger than `--max-filesize` fails with exit 63 (`CURLE_FILESIZE_EXCEEDED`) and curl 8.21.0's message: before `RETR` when `SIZE` tells the size, and part-way when it does not, as curl does.

## Context

- Conformance audit 2026-09-28, row 26 (Major): FTP ignores `--max-filesize`.
- `ITransferContext.MaxFileSize` exists; HTTP (FR-084, ADR-0044) and `file://` (FR-015) are the models. Only the FTP handler changes.
- `Record-CurlExchange.ps1 -Ftp` answers `SIZE` with `FtpData`'s length.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Ftp`: `--max-filesize` below and above the size, and with `SIZE` answered `550`; commands, stdout bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin commands, bytes written and outcome for each case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
