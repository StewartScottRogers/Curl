---
id: BL-5326
title: Report TFTP download size and speed in write-out
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Tftp.UnitLibrary/TftpTransferTimings.cs, Curl.Original.Protocol.Tftp.UnitLibrary/TftpDownload.cs, Curl.Original.Protocol.Tftp.UnitTests/TftpDownloadSizeReportTests.cs]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5326 — Report TFTP download size and speed in write-out

## Goal

A `tftp://` download reports its received bytes as `%{size_download}` and a non-zero `%{speed_download}`, as curl does, while keeping the timings BL-5102 added.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-077 (Low, Confirmed: Windows 3 runs incl. `--tftp-blksize`; Linux once). Area: TFTP download, write-out; Windows and Linux. A regression from the earlier timings fix (BL-5102, Done): `TftpTransferTimings.Stamp()` attaches a fresh `TransferReport`, so the download size is lost.

**Repro.** An RFC 1350/2347 TFTP server on 127.0.0.1:33069 serving `big.bin` (300000 bytes), written in PowerShell or as a C# file-based app (never Python). `curl -sS -o got.bin -w "sd=%{size_download} spd=%{speed_download} tt=%{time_total} tst=%{time_starttransfer}\n" tftp://127.0.0.1:33069/big.bin`

**Expected (reference curl).** `sd=300000`, a non-zero `spd` (e.g. 12298106), `tst` smaller than `tt`, exit 0. `%{json}` has `"size_download":300000`.

**Actual (port).** `sd=0 spd=0`, `tst` equal to `tt`, exit 0; `got.bin` is complete. `%{json}` has `"size_download":0,"size_delivered":0`. TFTP uploads report `size_upload` correctly.

**Where to start.** `Curl.Original.Protocol.Tftp.UnitLibrary/TftpTransferTimings.cs`, `Stamp()` (about line 33) attaches `new TransferReport()` with only `Timings`; `Curl.Output.UnitLibrary/WriteOutTransferInfo.cs:77` (`result.Report?.DownloadSize ?? result.BytesTransferred`) then reads 0. `TftpDownload.cs:410` returns `TransferResult.Success(bytesTransferred)` without `DownloadSize` (`TftpUpload.cs:388` does set `UploadSize`). Set `DownloadSize` in `Stamp()` (or in `TftpDownload`) for downloads. The `tst` equal to `tt` part is BL-5327, which depends on this task. Earlier related tasks (in `Tasks/Done`): BL-5026 (TFTP upload size), BL-5102 (TFTP timings).

## Acceptance criteria

- [x] New tests in `Curl.Original.Protocol.Tftp.UnitTests/TftpDownloadSizeReportTests.cs` prove that a TFTP download of N bytes (in-memory UDP stand-ins, including one with a negotiated `blksize`) returns a `TransferReport` with `DownloadSize` equal to N and `Timings` still set, and that the speed derived from them is non-zero with a fake advancing `TimeProvider`.
- [x] A test proves a failed download keeps its error exit code, and that the upload tests in `TftpUploadSizeReportTests.cs` and `TftpTransferTimingsTests.cs` stay green.
- [x] Tests are platform-neutral (stand-ins, no real network).
- [x] Hand check, recorded in the Log: the repro command prints `sd=300000` and a non-zero `spd`, and `%{json}` shows `"size_download":300000`, on Windows (and Linux if available).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command prints `sd=300000`, a non-zero `spd`, and `tst` < `tt`, on Windows and Linux (`tst` < `tt` is finished by BL-5327).

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 74c6e938 e4c7c582 51ca995c 8ae67a56 a0b49a29.
- 2026-11-16: Hand check on Windows (PowerShell UDP server, 300000 bytes): `sd=300000 spd=159065`, exit 0, file complete. `tst` still equals `tt` (BL-5327). Linux not available. Fix: `TftpDownload` sets `DownloadSize` on success; `Stamp()` keeps the report and adds timings. Quality run for the library: 0 failing members.
- 2026-11-16: Doing -> Done. TFTP download reports size_download and a non-zero speed
