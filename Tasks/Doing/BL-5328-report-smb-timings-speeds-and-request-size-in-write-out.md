---
id: BL-5328
title: Report SMB timings, speeds and request size in write-out
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Smb.UnitLibrary/SmbProtocolHandler.cs, Curl.Original.Protocol.Smb.UnitLibrary/SmbFileTransfer.cs, Curl.Original.Protocol.Smb.UnitTests/SmbWriteOutReportTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5328 — Report SMB timings, speeds and request size in write-out

## Goal

`smb://` and `smbs://` transfers report non-zero `%{time_*}` and `%{speed_*}` and a non-zero `%{size_request}`, as curl does.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-079 (Low, Confirmed twice on Windows and Linux). Area: smb://, smbs://, write-out.

Reference note: curl 8.22.0 with Schannel has SMB disabled (`curl: (1) Protocol "smb" is disabled`), so the review used curl 8.18 (the OpenSSL build) as the reference. The port enables SMB on purpose (ADR-1281).

**Repro.** An SMBv1 server on 127.0.0.1:33445 (the review used impacket `smbserver.py -port 33445 -username alice -password secret SHARE <dir>`; for our own stand-in use PowerShell or a C# file-based app, never Python).
- `curl -s -u alice:secret -o out.txt -w '%{json}' smb://127.0.0.1:33445/SHARE/hello.txt`
- `curl -sS -u alice:secret -T up.txt -w '%{size_upload} %{speed_upload} %{time_total}\n' smb://127.0.0.1:33445/SHARE/w.txt`

**Expected (curl 8.18).** Download JSON has `"size_request":520` and non-zero `speed_download`, `time_connect`, `time_pretransfer`, `time_starttransfer`, `time_total`; upload prints e.g. `13 3114 0.004174`.

**Actual (port).** `size_request` 0 and every `time_*`/`speed_*` 0; upload prints `13 0 0.000000`. Exit 0; sizes correct.

**Where to start.** `Curl.Original.Protocol.Smb.UnitLibrary/SmbProtocolHandler.cs`, `ExecuteAsync` attaches no `TransferReport.Timings` and no request byte count. Follow the `TftpTransferTimings` pattern (`Curl.Original.Protocol.Tftp.UnitLibrary/TftpTransferTimings.cs`: `context.TimeProvider`, `ConnectTimings`) and count bytes sent for `RequestSize`. `SmbFileTransfer.cs` (about line 83) builds the upload `TransferReport { UploadSize = written }`, which must be kept when timings are added. BL-5329 also changes `SmbFileTransfer.cs`; the two never run at once.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Smb.UnitTests/SmbWriteOutReportTests.cs`, driven by the existing fake SMB connection and a fake advancing `TimeProvider`, prove a download and an upload each return a `TransferReport` with non-zero connect, pretransfer, start-transfer and total timings, `RequestSize` equal to the bytes the stand-in saw the client send, and (upload) `UploadSize` unchanged.
- [ ] A test proves a failed transfer (for example a refused session setup) still returns its exit code with whatever timings were reached.
- [ ] Tests are platform-neutral (stand-ins, no real network).
- [ ] Hand check, recorded in the Log: both repro commands print non-zero `time_total`, `time_connect` and `speed_*`, and `size_request` > 0; if no SMB server is available the Log says so.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: both commands print non-zero `time_total`, `time_connect` and `speed_*`, and `size_request` > 0.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: fe77a9de 5174e641 51ca995c 5caf5fb1 a0b49a29.
