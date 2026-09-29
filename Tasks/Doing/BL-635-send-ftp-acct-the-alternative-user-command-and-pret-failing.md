---
id: BL-635
title: Send FTP ACCT, the alternative USER command and PRET, failing PRET with exit 84
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-634, BL-914]
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
- [ ] `ACCT` answered with anything but `230` ends with exit 11 (`CurlExitCode.FtpWeirdPassReply`) and `ACCT rejected by server: <code>`, pinned for `202` and `530` (measured in BL-662: `curl: (11) ACCT rejected by server: 530`; ADR-0216).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-29 (lane 3): `ITransferContext` has no member for `--ftp-account`, `--ftp-alternative-to-user` or `--ftp-pret`, and every other FTP option reaches the handler that way. As the Context says, the contract change is filed separately as BL-914 (Abstractions), which this task now depends on. Abstractions is also in BL-819's `touches` (Doing), so it cannot be done here.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Waits on BL-914: ITransferContext members for --ftp-account, --ftp-alternative-to-user and --ftp-pret
- 2026-09-29: Backlog -> Doing.
