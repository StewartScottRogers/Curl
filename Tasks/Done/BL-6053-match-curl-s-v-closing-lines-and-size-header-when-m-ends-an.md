---
id: BL-6053
title: Match curl's -v closing lines and size_header when -m ends an FTP data connect
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpNumHeadersTests.cs, Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Engine.UnitLibrary/AbortedHeadRecordingTransferEvents.cs, Curl.Engine.UnitTests/AbortedHeadRecordingTransferEventsTests.cs, Curl.Engine.UnitTests/FtpDataConnectMaxTimeTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
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

- [x] A unit test pins both `-v` lines, in curl's order, for `-m` passing during an FTP data connect.
- [x] A unit test pins `%{size_header}` for that case to what curl 8.22.0 reports.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `touches` widened: the 103 (141 with the replies measured here) came from
  `AbortedHeadRecordingTransferEvents`, which under `-m` counted every reply line as header bytes. No task in Doing on
  `origin/work/dark-factory` names the added files.
- `size_header`: that decorator now counts bytes only once an `HTTP/` or `RTSP/` status line has started a head on
  the latest connection, so a proxy's CONNECT reply still counts and no FTP, IMAP, POP3 or SMTP reply line does.
- `-v`: `FtpSession.DialDataAsync` puts `closing connection #N` on the cancellation
  (`Curl.Engine.ClosingConnectionLine`), and `TransferPerformer` writes the watchdog's message and that line when a
  watchdog (`-m` or `-Y`/`-y`) ended it. Only the data dial is marked; the reply waits are BL-6064.
- Tests: `FtpDataConnectMaxTimeTests` (both lines and `size_header` 0, end to end),
  `AbortedHeadRecordingTransferEventsTests`, `FtpNumHeadersTests`.
- Verified 2026-11-26: the Debug `curl.exe` prints the same two lines and `size_header` 0 as curl 8.22.0; build 0
  warnings; fast tests passed; `Measure-CodeQuality.ps1` for both libraries: 0 failing members.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 017a767e 604ebd71 3d836d5e.
- 2026-11-26: Doing -> Done. -m ending an FTP data connect writes curl's two closing -v lines and reports size_header 0
