---
id: BL-5101
title: Report time_* and speed_* write-out variables for file:// transfers
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs, Curl.Original.Protocol.File.UnitTests/FileTransferTimingsTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5101 — Report time_* and speed_* write-out variables for file:// transfers

## Goal

`file://` downloads and uploads report real, non-zero `%{time_namelookup}`, `%{time_pretransfer}`, `%{time_starttransfer}`, `%{time_total}`, `%{speed_download}` and `%{speed_upload}` in `-w`, as curl does.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-030 (Low, Confirmed; Windows and Linux builds), re-reported with status CHANGED: sizes are fixed (earlier tasks BL-5025 for `file://` and BL-5026 for TFTP, both Done), timings and speeds are still 0. This task is the `file://` part; the TFTP part is the separate task "Report time_* and speed_* write-out variables for tftp:// transfers".

**Repro.**
1. `head -c 300000 /dev/urandom > big.bin`
2. `F='spu=%{speed_upload} spd=%{speed_download} tt=%{time_total} tst=%{time_starttransfer} tpt=%{time_pretransfer} tn=%{time_namelookup}\n'`
3. `curl -sS -w "$F" -o NUL "file:///$(cygpath -m $PWD)/big.bin"`
4. `curl -sS -w "$F" -T big.bin "file:///$(cygpath -m $PWD)/w.out"`

**Expected (curl 8.22.0).** Non-zero values in every line, e.g. FILE download `spd=527240773 tt=0.000578 tst=0.000441 tpt=0.000555 tn=0.000096`.

**Actual.** Every line prints `spu=0 spd=0 tt=0.000000 tst=0.000000 tpt=0.000000 tn=0.000000`, exit 0. FTP, SFTP and SCP report real timings.

**Where to start.** `TransferReport.Timings` (`Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs`, about line 181) is set only by FtpSession, HttpProtocolHandler and SshProtocolHandler (see `SshTransferClock` in `Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs` for a model: it uses `context.TimeProvider` and a `ConnectTimings`). `Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs` builds `TransferReport` without `Timings` (about lines 665 and 832); `Curl.Output.UnitLibrary/WriteOutTransferInfo.cs` `WithTimesAndSpeeds` then returns 0. Use the injected `TimeProvider`, never a real clock. If a shared contract change in `Curl.Protocol.Abstractions` proves necessary, stop and file it as its own task rather than widening this one.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.File.UnitTests/FileTransferTimingsTests.cs`, driven by a fake `TimeProvider` that advances between steps, prove that a file download and a file upload produce a `TransferReport` whose timings give non-zero total, pretransfer and start-transfer times and a non-zero connect/namelookup value consistent with curl's FILE output (all of them non-zero, as in the repro), and speeds equal size divided by total time.
- [ ] A test proves a failed `file://` transfer still reports whatever timings curl would, and size reporting from BL-5025 is unchanged (existing tests green).
- [ ] Tests are platform-neutral (drive-less URLs, temp files).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; this task needs none. Any hand-check helper is PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python.
- Review acceptance check, for a hand check: both `file://` commands print non-zero `tt`, `tpt` and `tn`; the download prints non-zero `spd` and the upload non-zero `spu`.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: df4224e1 488eb91f 1feb8c58 a0b49a29.
