---
id: BL-782
title: Send SYST after a PWD reply naming a relative directory
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-782 — Send SYST after a PWD reply naming a relative directory

## Goal

After a `257` reply to `PWD` whose quoted directory does not start with `/`, the FTP handler sends `SYST` as curl 8.21.0 does, and follows a `215 OS/400` reply with `SITE NAMEFMT 1`.

## Context

- Found while measuring BL-514 on 2026-09-28: `Record-CurlExchange.ps1 -Ftp -FtpReply 'PWD=257 "home" is cwd'` shows curl sending `SYST` right after `PWD` (answered `502` by the recorder, and curl carried on with `EPSV`, exit 0, `%{ftp_entry_path}` = `home`). `FtpSession` (`Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`, `TransferPathAsync`) sends no `SYST`.
- curl's `ftp_state_pwd_resp` / `ftp_state_syst_resp` in lib/ftp.c: `SYST` only when no server OS is known yet; a `215` reply starting `OS/400` sends `SITE NAMEFMT 1` and then `PWD` again.
- `FtpEntryPath` already extracts the directory (BL-514).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Ftp`: `PWD=257 "home"` with `SYST` answered `502`, `215 UNIX Type: L8` and `215 OS/400 is the remote operating system`; the transcript, stdout, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` pin the commands sent for each measured case, and that a `/`-rooted directory sends no `SYST`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Ftp.UnitLibrary`.

## Notes

- Measured 2026-09-29, curl 8.21.0 (Schannel, mingw64), `Record-CurlExchange.ps1 -Ftp -FtpData hello -CurlArgs ftp://127.0.0.1:18221/file.txt,-w,[%{ftp_entry_path}]`. Every case: exit 0, stderr empty, stdout `hello[home]` (`hello[/home]` when rooted). Commands after `USER anonymous`, `PASS ftp@example.com`, `PWD`:
  - `PWD=257 "home" is cwd` (SYST answered `502 Command not implemented`): `SYST`, `EPSV`, `TYPE I`, `SIZE file.txt`, `RETR file.txt`, `QUIT`.
  - `SYST=215 UNIX Type: L8`: `SYST`, `EPSV`, ... - the same.
  - `SYST=215 OS/400 is the remote operating system` (SITE answered `502`): `SYST`, `SITE NAMEFMT 1`, `EPSV`, ...
  - The same with `SITE=250 ok`: `SYST`, `SITE NAMEFMT 1`, `PWD` (257 "home" again), `EPSV`, ... - no second `SYST`.
  - `PWD=257 "/home" is cwd`: no `SYST`; `EPSV` straight after `PWD`.
- Implementation: `FtpSession.ReadEntryPathAsync` sends `SYST` for a relative entry path; `FtpServerSystem.IsOs400` reads a `215`'s first word (after the code and any spaces, case-insensitive, as curl's `ftp_state_syst_resp` with `curl_strequal`); a 2xx to `SITE NAMEFMT 1` re-reads the entry path with no second `SYST`. Tests: `FtpProtocolHandlerServerSystemTests`.
- No ADR: no design choice beyond matching the measured bytes. Quality: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` 100% line, 100% branch, 0 failing of 190 members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A PWD reply naming a relative directory sends SYST; 215 OS/400 sends SITE NAMEFMT 1 and PWD again, as curl 8.21.0
