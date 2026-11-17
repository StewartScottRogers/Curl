---
id: BL-5327
title: Stamp FILE and TFTP upload start-transfer time at the start of the upload
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5326]
touches: [Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs, Curl.Original.Protocol.File.UnitTests/FileTransferTimingsTests.cs, Curl.Original.Protocol.Tftp.UnitLibrary/TftpTransferTimings.cs, Curl.Original.Protocol.Tftp.UnitLibrary/TftpUpload.cs, Curl.Original.Protocol.Tftp.UnitTests/TftpTransferTimingsTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5327 — Stamp FILE and TFTP upload start-transfer time at the start of the upload

## Goal

A `file://` upload and a `tftp://` upload report `%{time_starttransfer}` as `0.000000` (or a value below `%{time_total}` by curl's rule), not equal to `%{time_total}`.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-030 (Low, Confirmed twice), re-reported with status CHANGED ("FILE/TFTP timings and speeds now fixed"): the earlier fixes are incomplete. Earlier tasks, all in `Tasks/Done`: BL-5101 (time and speed variables for file:// transfers), BL-5102 (the same for tftp://), BL-5025 and BL-5026 (upload sizes). Area: FILE and TFTP uploads, write-out, Windows. Depends on BL-5326 because both change `TftpTransferTimings.cs`.

**Repro.** (any 300 KB file `big.bin`)
- `curl -sS -T big.bin -w "tst=%{time_starttransfer} tt=%{time_total}\n" file:///C:/temp/w.out`
- `curl -sS -T big.bin -w "tst=%{time_starttransfer} tt=%{time_total}\n" tftp://127.0.0.1:33069/up.bin` against a TFTP server (PowerShell or C# file-based app, never Python).

**Expected (reference curl).** `tst=0.000000` and a non-zero `tt`.

**Actual (port).** `tst` equals `tt` (e.g. `tst=0.062701 tt=0.062701`).

**Where to start.** The FILE upload path in `Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs` (`WithTimings`, about line 205, builds `TransferTimings` with start-transfer = `completed`) and `Curl.Original.Protocol.Tftp.UnitLibrary/TftpTransferTimings.cs` `Stamp()` (start-transfer = `completed` on success) set the start-transfer stamp at the end of the upload. For an upload curl reports start-transfer as 0; downloads keep their stamp. `Stamp()` (called from `TftpUpload.cs:92` and `TftpDownload.cs:93`) needs to know the direction. Note the FILE upload path (about line 847) replaces the report with `new TransferReport { UploadSize = ... }`; check the upload still gets its timings.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.File.UnitTests/FileTransferTimingsTests.cs` prove a `file://` upload's `TransferTimings` has a zero/absent start-transfer and a non-zero total, and a `file://` download is unchanged.
- [ ] New tests in `Curl.Original.Protocol.Tftp.UnitTests/TftpTransferTimingsTests.cs` prove the same for a TFTP upload (fake advancing `TimeProvider`, in-memory UDP stand-ins) and that a TFTP download keeps a start-transfer below its total.
- [ ] Tests are platform-neutral (drive-less `file:///dir/x` URL, stand-ins).
- [ ] Hand check, recorded in the Log: both repro commands print `tst=0.000000` with a non-zero `tt`; measure real curl with `Record-CurlExchange.ps1` for the FILE case and say which curl version.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: both commands print `tst=0.000000` (or a value smaller than `tt` that matches the reference's rule) with a non-zero `tt`.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 99955f53 a365cfcb 79ab3049 821941df a0b49a29.
