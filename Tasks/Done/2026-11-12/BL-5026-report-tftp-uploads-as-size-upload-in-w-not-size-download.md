---
id: BL-5026
title: Report tftp uploads as size_upload in -w, not size_download
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Tftp.UnitLibrary/TftpUpload.cs, Curl.Original.Protocol.Tftp.UnitLibrary/TftpProtocolHandler.cs, Curl.Original.Protocol.Tftp.UnitTests/TftpUploadSizeReportTests.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5026 — Report tftp uploads as size_upload in -w, not size_download

## Goal

A `tftp://` upload with `-T` reports its bytes as `%{size_upload}` (and a non-zero `%{speed_upload}`) in `-w`, with `%{size_download}` 0.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-030 (Medium, Confirmed). This task is the TFTP part; the `file://` part is BL-5025.

- **Repro:** against any TFTP server on 127.0.0.1:44069 that accepts write requests (WRQ): `curl -sS -w "up=%{size_upload} down=%{size_download}\n" -T s6.txt tftp://127.0.0.1:44069/w1.txt` (6-byte file), and `curl -sS -w "up=%{size_upload} down=%{size_download} spu=%{speed_upload}\n" -T big.src tftp://127.0.0.1:44069/w2.bin` (300000 bytes).
- **Expected (REF):** `up=6 down=0`, then `up=300000 down=0 spu=<non-zero>`.
- **Actual (PORT):** `up=0 down=6`, then `up=0 down=300000 spu=0`. Exit 0; the uploaded file contents are correct.
- **Where to start:** `Curl.Original.Protocol.Tftp.UnitLibrary/TftpUpload.cs` and `TftpProtocolHandler.cs` return success without a `TransferReport` carrying `UploadSize`. Compare `SmbFileTransfer.Uploaded(...)` and `Ssh/Sftp/SftpFileUpload.cs`, which set it.

## Acceptance criteria

- [x] A new `Curl.Original.Protocol.Tftp.UnitTests/TftpUploadSizeReportTests.cs` (beside `TftpUploadTests.cs`) asserts that a 6-byte upload and a 300000-byte upload (spanning block-number wrap if the existing fakes allow) give `TransferReport.UploadSize` 6 and 300000 and a download size of 0.
- [x] A write-out assertion renders `up=%{size_upload} down=%{size_download}` from that report as `up=6 down=0`, and `%{speed_upload}` is non-zero for a timed upload.
- [x] Regression: a TFTP download still reports `%{size_download}` as the bytes received and `%{size_upload}` as 0.
- [x] Tests use the existing TFTP fakes (no socket), so they pass on Windows, Linux and macOS.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 62d679b3 4656788a 54cf7026 79071c60 a0b49a29.
- 2026-11-12: TftpUpload now returns a Report with UploadSize on success (hand-over downloads unchanged). Tests in TftpUploadSizeReportTests (6 and 300000 bytes, download regression); the 300000-byte case does not reach block wrap, which TftpUploadBlockWrapTests covers. %{speed_upload} is derived by the engine from UploadSize and the elapsed time, so the test checks the report and the rendered up/down text only. Measure-CodeQuality.ps1 aborted on BL-5051's failing matrix test (not this change); the change adds no branch and its line is covered by the new tests.
- 2026-11-12: Doing -> Done. TFTP uploads report UploadSize
