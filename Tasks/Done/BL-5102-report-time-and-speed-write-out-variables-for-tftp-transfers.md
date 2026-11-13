---
id: BL-5102
title: Report time_* and speed_* write-out variables for tftp:// transfers
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Tftp.UnitLibrary/TftpTransferTimings.cs, Curl.Original.Protocol.Tftp.UnitLibrary/TftpUpload.cs, Curl.Original.Protocol.Tftp.UnitLibrary/TftpDownload.cs, Curl.Original.Protocol.Tftp.UnitTests/TftpTransferTimingsTests.cs]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5102 — Report time_* and speed_* write-out variables for tftp:// transfers

## Goal

`tftp://` downloads and uploads report real, non-zero `%{time_*}` and `%{speed_*}` write-out variables, as curl does.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-030 (Low, Confirmed; Windows and Linux builds), re-reported with status CHANGED: sizes are fixed (earlier tasks BL-5025 for `file://` and BL-5026 for TFTP, both Done), timings and speeds are still 0. This task is the TFTP part; the `file://` part is the separate task "Report time_* and speed_* write-out variables for file:// transfers".

**Repro.**
1. `head -c 300000 /dev/urandom > big.bin`
2. `F='spu=%{speed_upload} spd=%{speed_download} tt=%{time_total} tst=%{time_starttransfer} tpt=%{time_pretransfer} tn=%{time_namelookup}\n'`
3. With an RFC 1350 TFTP server on 127.0.0.1:23069 that serves `big.bin` and accepts write requests (PowerShell or a C# file-based app, not Python): `curl -sS -w "$F" -o NUL tftp://127.0.0.1:23069/big.bin` and `curl -sS -w "$F" -T big.bin tftp://127.0.0.1:23069/w2.bin`

**Expected (curl 8.22.0).** Non-zero values, e.g. TFTP upload `spu=5690008 tt=0.052804 tpt=0.052721`.

**Actual.** `spu=0 spd=0 tt=0.000000 tst=0.000000 tpt=0.000000 tn=0.000000`, exit 0. FTP, SFTP and SCP report real timings.

**Where to start.** `TransferReport.Timings` (`Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs`, about line 181) is set only by FtpSession, HttpProtocolHandler and SshProtocolHandler (`SshTransferClock` in `Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs` is a model: it uses `context.TimeProvider` and `ConnectTimings`). `Curl.Original.Protocol.Tftp.UnitLibrary/TftpUpload.cs` (about line 382) and `TftpDownload.cs` build `TransferReport` without `Timings`; `Curl.Output.UnitLibrary/WriteOutTransferInfo.cs` `WithTimesAndSpeeds` then returns 0. Use the injected `TimeProvider`, never a real clock. If a shared contract change in `Curl.Protocol.Abstractions` proves necessary, file it as its own task rather than widening this one. Other handlers (DICT, Gopher, Telnet, mail, LDAP, MQTT) may share the gap but were not tested; do not widen this task to them.

## Acceptance criteria

- [x] New tests in `Curl.Original.Protocol.Tftp.UnitTests/TftpTransferTimingsTests.cs`, driven by a fake `TimeProvider` that advances, prove a TFTP download and a TFTP upload produce a `TransferReport` with non-zero total, pretransfer, start-transfer and name-lookup/connect timings, and speeds equal size divided by total time.
- [x] Existing size-report tests (`TftpUploadSizeReportTests.cs`) stay green.
- [x] Tests are platform-neutral (in-memory UDP stand-ins).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; use PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python.
- Review acceptance check, for a hand check: both `tftp://` commands print non-zero `tt`, `tpt` and `tn`; the download prints non-zero `spd` and the upload non-zero `spu`.

- Done: new TftpTransferTimings stamps Report.Timings in TftpDownload/TftpUpload.RunAsync (new file added to touches). Decision: the datagram channel is open before the transfer starts, so namelookup, connect and pretransfer share one timestamp; starttransfer = completion on success (null on failure). Quality run: Tftp library 100% line and branch, 0 failing members.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 7a9a7505 957057ea 860f6216 a0b49a29.
- 2026-11-13: Doing -> Done. tftp:// transfers report time_* and speed_* write-out variables
