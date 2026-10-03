---
id: BL-1249
title: Write curl's Remembering and left-intact -v lines for every FTP failure that keeps the control connection
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1240]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1249 — Write curl's Remembering and left-intact -v lines for every FTP failure that keeps the control connection

## Goal

Every FTP failure that curl 8.21.0's `ftp_done` treats as leaving the control connection usable writes its `* Remembering we are in directory "..."` and `* Connection #0 to host ... left intact` `-v` lines before the transfer ends, as the unreadable-`229` path does since BL-1240.

## Context

- BL-1240 added `FtpSession.QuitAndFailKeepingConnectionAsync` (`Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`) and uses it only for an unreadable `229` (exit 13); every other `QuitAndFailAsync` caller writes neither line.
- curl 8.21.0 `lib/ftp.c` `ftp_done`: for `CURLE_BAD_DOWNLOAD_RESUME`, `CURLE_FTP_WEIRD_PASV_REPLY`, `CURLE_FTP_PORT_FAILED`, `CURLE_FTP_ACCEPT_FAILED`, `CURLE_FTP_ACCEPT_TIMEOUT`, `CURLE_FTP_COULDNT_SET_TYPE`, `CURLE_FTP_COULDNT_RETR_FILE`, `CURLE_PARTIAL_FILE`, `CURLE_UPLOAD_FAILED`, `CURLE_REMOTE_ACCESS_DENIED`, `CURLE_FILESIZE_EXCEEDED`, `CURLE_REMOTE_FILE_NOT_FOUND` and `CURLE_WRITE_ERROR` the connection "stays alive fine"; any other failure marks it invalid.
- Measured 2026-10-02 for `(|||99999|)` (BL-1240 Notes): after the failure's `-v` line, `* Remembering we are in directory ""` and `* Connection #0 to host 127.0.0.1:<port> left intact`.

## Acceptance criteria

- [ ] Before the code change, Notes hold `Record-CurlExchange.ps1 -Ftp` `-v` recordings for at least `PASV=500 no` after a refused EPSV (exit 13), `TYPE=500 no` (exit 17) and `SIZE=550 no` (exit 78).
- [ ] Tests in `Curl.Protocol.Ftp.UnitTests` pin each measured case's `-v` lines in order.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
