---
id: BL-1251
title: Write curl's Remembering and left-intact -v lines for every FTP failure that keeps the control connection
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1240]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1251 — Write curl's Remembering and left-intact -v lines for every FTP failure that keeps the control connection

## Goal

Every FTP failure that curl 8.21.0's `ftp_done` treats as leaving the control connection usable writes its `* Remembering we are in directory "..."` and `* Connection #0 to host ... left intact` `-v` lines before the transfer ends, as the unreadable-`229` path does since BL-1240.

## Context

- BL-1240 added `FtpSession.QuitAndFailKeepingConnectionAsync` (`Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`) and uses it only for an unreadable `229` (exit 13); every other `QuitAndFailAsync` caller writes neither line.
- curl 8.21.0 `lib/ftp.c` `ftp_done`: for `CURLE_BAD_DOWNLOAD_RESUME`, `CURLE_FTP_WEIRD_PASV_REPLY`, `CURLE_FTP_PORT_FAILED`, `CURLE_FTP_ACCEPT_FAILED`, `CURLE_FTP_ACCEPT_TIMEOUT`, `CURLE_FTP_COULDNT_SET_TYPE`, `CURLE_FTP_COULDNT_RETR_FILE`, `CURLE_PARTIAL_FILE`, `CURLE_UPLOAD_FAILED`, `CURLE_REMOTE_ACCESS_DENIED`, `CURLE_FILESIZE_EXCEEDED`, `CURLE_REMOTE_FILE_NOT_FOUND` and `CURLE_WRITE_ERROR` the connection "stays alive fine"; any other failure marks it invalid.
- Measured 2026-10-02 for `(|||99999|)` (BL-1240 Notes): after the failure's `-v` line, `* Remembering we are in directory ""` and `* Connection #0 to host 127.0.0.1:<port> left intact`.

## Acceptance criteria

- [x] Before the code change, Notes hold `Record-CurlExchange.ps1 -Ftp` `-v` recordings for at least `PASV=500 no` after a refused EPSV (exit 13), `TYPE=500 no` (exit 17) and `SIZE=550 no` (exit 78).
- [x] Tests in `Curl.Protocol.Ftp.UnitTests` pin each measured case's `-v` lines in order.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-02 before the code change, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -OutDirectory <dir> -Port 18931 -Ftp -FtpIdleMilliseconds 2000 -FtpReply <overrides> -CurlArgs '-v','ftp://127.0.0.1:18931/f.txt'` (`-FtpData 'hello'` for the RETR cases). Each `-v` tail after the failing reply, then the commands sent:
  - `EPSV=500 no`, `PASV=500 no`: exit 13. `< 500 no` / `* Failed EPSV attempt. Disabling EPSV` / `> PASV` / `< 500 no` / `* Bad PASV/EPSV response: 500` / `* Remembering we are in directory ""` / `* Connection #0 to host 127.0.0.1:18931 left intact`. Commands `USER`, `PASS`, `PWD`, `EPSV`, `PASV`, `QUIT`.
  - `TYPE=500 no`: exit 17. `> TYPE I` / `< 500 no` / `* Could not set desired mode` / `* Remembering we are in directory ""` / `* Connection #0 ... left intact`. Commands end `EPSV`, `TYPE I`, `QUIT`.
  - `SIZE=550 no`: exit 78. `> SIZE f.txt` / `< 550 no` / `* The file does not exist` / `* Remembering ...` / `* Connection #0 ... left intact`. Commands end `SIZE f.txt`, `QUIT`.
  - `RETR=550 no`: exit 78, `* RETR response: 550`, then the same two lines.
  - `CWD=550 no` on `/d/f.txt`: exit 9, `* Server denied you to change to the given directory` then only `* Connection #0 ... left intact` (no `Remembering` line).
  - `EPRT=500 no`, `PORT=500 no` with `-P 127.0.0.1`: exit 30, `* Failed to do PORT` / `* Remembering ...` / `* Connection #0 ... left intact`.
  - `-P 127.0.0.1:18931` (the control port, so the bind fails): exit 30, `* bind() failed, ran out of ports` then only `* Connection #0 ... left intact`.
  - `RETRDONE=451 no` / `RETRDONE=552 full`: exit 18 / 70. `* Remembering ...` (before the reply, as already implemented) / `< 451 no` / `* server did not report OK, got 451` (or `* Exceeded storage allocation`) / `* Connection #0 ... left intact`. Exit 70 is left intact too: `ftp_done` sees status OK and only then turns the reply into the error.
  - `-C 200`, `SIZE=213 5`: exit 36, `* Offset (200) was beyond file size (5)` / `* Remembering ...` / `* Connection #0 ... left intact`.
  - `--max-filesize 50`, `SIZE=213 100`: exit 63, same two lines. With `-r 0-2` as well: `* Maximum file size exceeded` / `* Remembering ...` / `> ABOR` / `< 502 ...` / `* partial download completed, closing connection` / `* shutting down connection #0`, then `QUIT`; not left intact.
- Done: `QuitAndFailAsync` now writes `Remembering` and `left intact` before `QUIT` (the old `QuitAndFailKeepingConnectionAsync` is folded into it); a new `QuitAndFailLeavingConnectionIntactAsync` writes only `left intact`, for a refused `CWD`, an unbindable `-P` port and a `451`/`552` end-of-transfer reply; `EndAndFailAsync` with a byte limit writes the ABOR sequence above.
- Choice (sensible default): no exit-code set inside `QuitAndFailAsync`. Every one of its callers passes a code from curl's keep list (bind and accept failures are always 30 and 10 from `TcpConnectionListener`/`TcpPendingConnection`), so a set check's other branch could never run and would fail the 100% branch gate; the doc comment names the list instead.
- Tests: `FtpProtocolHandlerDataConnectionEventTests` pins exits 13, 17, 78 (SIZE and RETR), 9, 18, 70, 36, 63 and 63-with-range; `FtpProtocolHandlerActiveModeTests` pins the three bind-failure cases. `MutableContext` (test fake) gained `MaxFileSize`.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Every FTP failure curl keeps the control connection for writes its Remembering and left-intact -v lines as measured (exits 9, 13, 17, 18, 30, 36, 63, 70, 78)
