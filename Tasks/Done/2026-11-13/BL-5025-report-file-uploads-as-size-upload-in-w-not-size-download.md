---
id: BL-5025
title: Report file:// uploads as size_upload in -w, not size_download
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs, Curl.Original.Protocol.File.UnitTests/FileProtocolHandlerUploadSizeTests.cs, Curl.Output.UnitTests/TransferWriteOutVariablesTests.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5025 — Report file:// uploads as size_upload in -w, not size_download

## Goal

A `file://` upload with `-T` reports its bytes as `%{size_upload}` (and a non-zero `%{speed_upload}`) in `-w`, with `%{size_download}` 0.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-030 (Medium, Confirmed). This task is the `file://` part; the TFTP part is BL-5026.

- **Repro:** `printf 'HELLO\n' > s6.txt` then `curl -sS -w "up=%{size_upload} down=%{size_download} speed_up=%{speed_upload}\n" -T s6.txt file:///<dir>/w.out`
- **Expected (REF):** `up=6 down=0 speed_up=<non-zero>`. FTP, SFTP and SCP uploads already report this correctly.
- **Actual (PORT):** `up=0 down=6 speed_up=0`. Exit 0; the uploaded file contents are correct.
- **Where to start:** `Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs`: `UploadIntoAsync` and `UploadIntoDestinationAsync` return `TransferResult.Success(transferred)` without `Report = new TransferReport { UploadSize = ... }`. Compare `SmbFileTransfer.Uploaded(...)` and `Ssh/Sftp/SftpFileUpload.cs`, which set `UploadSize`.

## Acceptance criteria

- [x] A new test class `Curl.Original.Protocol.File.UnitTests/FileProtocolHandlerUploadSizeTests.cs` uploads a 6-byte source through `FileProtocolHandler` and asserts the result's `TransferReport.UploadSize` is 6 and its download size is 0.
- [x] A write-out test (in that class, or in `Curl.Output.UnitTests/TransferWriteOutVariablesTests.cs`) renders `up=%{size_upload} down=%{size_download}` from that report as `up=6 down=0`, and `%{speed_upload}` is non-zero for a timed upload.
- [x] Regression: a `file://` download still reports `%{size_download}` as the bytes read and `%{size_upload}` as 0; the uploaded file contents are unchanged.
- [x] Tests use drive-less `file:///dir/x` URLs or the handler's injected file abstractions, so they pass on Windows, Linux and macOS.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text.

## Log

- Notes: UploadAsync now attaches a TransferReport with UploadSize; the write-out test went in Curl.Output.UnitTests (added to touches); the existing upload "ReportsNoHeaders" test now expects a report with no pseudo-headers. Platform-neutral drive-less URLs. Quality run: 0 failing members.

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 2c6e2a92 a0ad802a c93d26ed fc55c716 a0b49a29.
- 2026-11-12: Doing -> Done. file:// uploads report UploadSize; tests added, build and fast tests green
