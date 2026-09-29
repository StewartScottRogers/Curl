---
id: BL-635
title: Send FTP ACCT, the alternative USER command and PRET, failing PRET with exit 84
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-634]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-635 — Send FTP ACCT, the alternative USER command and PRET, failing PRET with exit 84

## Goal

The FTP handler sends `ACCT <account>` when the server answers `332` and `--ftp-account` is given, retries a refused `USER` with the `--ftp-alternative-to-user` command, and sends `PRET <command>` before `PASV`/`EPSV` with `--ftp-pret`, failing a refused `PRET` with exit 84 (`CURLE_FTP_PRET_FAILED`), each as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 25 (Major). Options: BL-634.
- Code: `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`; context mapping in `Curl.Console/TransferContextFactory.cs`. New `ITransferContext` members belong in Abstractions: if needed, file an Abstractions task, depend on it, and keep this task to FTP and Console.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Ftp -FtpReply`: `PASS` answered `332` with and without `--ftp-account`, `USER` answered `530` with `--ftp-alternative-to-user "USER alt"`, `--ftp-pret` with `PRET` answered `200` and `500`, for a download and a listing; commands, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin commands and outcome for each case.
- [ ] `ACCT` answered with anything but `230` ends with exit 11 (`CurlExitCode.FtpWeirdPassReply`) and `ACCT rejected by server: <code>`, pinned for `202` and `530` (measured in BL-662: `curl: (11) ACCT rejected by server: 530`; ADR-0215).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
