---
id: BL-781
title: Send SYST after a PWD reply naming a relative directory
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-781 — Send SYST after a PWD reply naming a relative directory

## Goal

After a `257` reply to `PWD` whose quoted directory does not start with `/`, the FTP handler sends `SYST` as curl 8.21.0 does, and follows a `215 OS/400` reply with `SITE NAMEFMT 1`.

## Context

- Found while measuring BL-514 on 2026-09-28: `Record-CurlExchange.ps1 -Ftp -FtpReply 'PWD=257 "home" is cwd'` shows curl sending `SYST` right after `PWD` (answered `502` by the recorder, and curl carried on with `EPSV`, exit 0, `%{ftp_entry_path}` = `home`). `FtpSession` (`Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`, `TransferPathAsync`) sends no `SYST`.
- curl's `ftp_state_pwd_resp` / `ftp_state_syst_resp` in lib/ftp.c: `SYST` only when no server OS is known yet; a `215` reply starting `OS/400` sends `SITE NAMEFMT 1` and then `PWD` again.
- `FtpEntryPath` already extracts the directory (BL-514).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Ftp`: `PWD=257 "home"` with `SYST` answered `502`, `215 UNIX Type: L8` and `215 OS/400 is the remote operating system`; the transcript, stdout, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin the commands sent for each measured case, and that a `/`-rooted directory sends no `SYST`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Ftp.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
