---
id: BL-1198
title: Write the --trace-config ftp lines for PRET, SITE NAMEFMT and MKD
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1198 — Write the --trace-config ftp lines for PRET, SITE NAMEFMT and MKD

## Goal

Under `-v --trace-config ftp`, `--ftp-pret` (`PRET`), the `SITE NAMEFMT 1` curl sends after `SYST` answered `215 OS/400`, and `--ftp-create-dirs` (`MKD`) write curl 8.21.0's `[FTP]` state lines.

## Context

- Follow-up of BL-1197, whose Notes hold the rules measured so far. `FtpStateTrace.StateOf` (`Curl.Protocol.Ftp.UnitLibrary`) maps these three commands to no state, so they write no state change.
- Also unmeasured there: a `-r` range whose `-` post-quote is refused (Curl writes `done, result=0` before the quotes).
- Measure with `Record-CurlExchange.ps1 -Ftp -FtpReply` (e.g. `'SYST=215 OS/400'`, `'CWD=550 no'` with `--ftp-create-dirs`) before pinning.

## Acceptance criteria

- [ ] Tests in `FtpProtocolHandlerStateTraceTests` pin the measured `[FTP]` lines for `--ftp-pret`, `SITE NAMEFMT 1` and `--ftp-create-dirs`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
