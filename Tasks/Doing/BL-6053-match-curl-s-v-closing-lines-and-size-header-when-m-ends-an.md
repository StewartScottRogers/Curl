---
id: BL-6053
title: Match curl's -v closing lines and size_header when -m ends an FTP data connect
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpNumHeadersTests.cs, Curl.Engine.UnitLibrary/TransferPerformer.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6053 — Match curl's -v closing lines and size_header when -m ends an FTP data connect

## Goal

When `-m` ends an FTP transfer during the passive data connect, `-v` and `%{size_header}` match curl 8.22.0.

## Context

Found by BL-6016. Measured 2026-11-26 with `Record-CurlExchange.ps1 -Ftp`, replies `EPSV=500 no: refused` and
`PASV=227 Entering: (10,255,255,1,10,10)`, arguments `-sS -v -m 8 --no-ftp-skip-pasv-ip`: curl 8.22.0 (Windows) writes
`* Operation timed out after N milliseconds with 0 bytes received` and `* closing connection #0` before the
`curl: (28)` line and reports `%{size_header}` 0; Curl writes neither `-v` line and reports 103. BL-6016 changed
neither. Start at `TransferPerformer.FinishedByWatchdogAsync` and `FtpSession.RunAsync`'s cancellation catch.

## Acceptance criteria

- [ ] A unit test pins both `-v` lines, in curl's order, for `-m` passing during an FTP data connect.
- [ ] A unit test pins `%{size_header}` for that case to what curl 8.22.0 reports.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 017a767e 604ebd71 3d836d5e.
