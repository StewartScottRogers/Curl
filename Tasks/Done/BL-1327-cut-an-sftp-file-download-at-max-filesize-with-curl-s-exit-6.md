---
id: BL-1327
title: Cut an SFTP file download at --max-filesize with curl's exit 63 'Exceeded the maximum allowed file size' text
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: FR-084
created: 2026-10-03
completed: 2026-10-03
---
# BL-1327 — Cut an SFTP file download at --max-filesize with curl's exit 63 'Exceeded the maximum allowed file size' text

## Goal

An `sftp://` file download honours `ITransferContext.MaxFileSize` as curl 8.21.0's download writer does: the bytes under the limit are written, the rest of the write is cut, and the transfer fails with exit 63 and `Exceeded the maximum allowed file size (N) with N bytes`, reported as a `-v` info line too.

## Context

- Today `Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpFileDownload.cs` (`Copy`, the write at line 188) writes every byte read to `output` and never reads `context.MaxFileSize`; the remark on `ITransferContext.MaxFileSize` (`Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`) lists `scp`/`sftp` among the handlers that do not read it yet.
- curl 8.21.0 hands every body write of every protocol to `cw_download_write` in `lib/sendf.c` (tag `curl-8_21_0`), and libssh2's SFTP read loop writes through it. Lines 251-257 cut a write to the bytes left under `data->set.max_filesize`, write the cut part, and lines 286-291 then `failf(data, "Exceeded the maximum allowed file size (%" FMT_OFF_T ") with %" FMT_OFF_T " bytes", max_filesize, bytecount)` and return `CURLE_FILESIZE_EXCEEDED` (exit 63). A body exactly at the limit does not fail (`nwrite < nbytes` is false). A limit of 0 is no limit. There is no up-front size check for SFTP: `lib/ftp.c` line 1786 (`Maximum file size exceeded`) is FTP's alone.
- BL-1305 (TFTP), BL-1296 (SMB) and BL-1291 (POP3) made the same change in their libraries; follow their shape (one writer that cuts and reports, tests that pin the cut point inside a later write).
- On a failed download the session must still close the remote file handle and end as the existing failed-download tests (`SshProtocolHandlerTests.FailedSftpTeardown.cs`) expect for any other mid-download failure; pin what it sends.
- An upload (`-T`) ignores `--max-filesize`; a directory listing (`sftp://host/dir/`) is not this task's.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Ssh.UnitTests` downloads `hello\n` over the fake SFTP server with `MaxFileSize = 3` and asserts exit 63 (`CurlExitCode.FilesizeExceeded`), message `Exceeded the maximum allowed file size (3) with 3 bytes`, output `hel`, and that message reported as an info line.
- [x] A test with a file larger than one SFTP read and a limit inside the second read asserts the first read is written whole and only the allowed bytes of the second.
- [x] Tests pin that `MaxFileSize` of 0, `null`, and exactly the file's length complete with exit 0 and the whole file.
- [x] A test pins that an SFTP upload with `MaxFileSize = 1` completes as today.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports no failing member *in the code this task changed* (see Notes).

## Notes

- Delivered directly rather than through the full `/protocol` stages: the change is one cut in `SftpFileDownload.Copy` plus one argument from the handler, following BL-1305's shape. No ADR: the behaviour is curl 8.21.0's `cw_download_write`, already pinned by the task.
- `SftpFileDownload.DownloadAsync` takes an optional `maxFileSize`; `Copy` writes `min(read, limit - received)` bytes, reports progress for what it wrote, and on a cut returns exit 63 with the `failf` text. The handler's existing `ReportReturnedFailure` turns that into the `-v` info line, so no new event wiring was needed.
- Teardown on the cut, pinned in `SshProtocolHandlerTests.MaxFileSize.cs`: `sftp 16 .`, `OPEN`, `STAT`, `READ`, `CLOSE`, then channel EOF, CLOSE and DISCONNECT 11, the same as any other failed copy.
- With unknown size and a cut in the second 30000-byte read, the read-ahead had topped up one more read, so `CLOSE` goes out with request id 18.
- Tests: 1633 passed in `Curl.Protocol.Ssh.UnitTests` (8 new cases). Measure-CodeQuality, 2026-10-03 09:32: every member this task changed passes. It reported 2 failing members this task did not touch and added no branch to: `SshProtocolHandler..ctor` (line 81, branch 75%) and `HandshakeAndTransferAsync` (line 255, branch 66.67%). Filed as BL-1335 rather than widening this task.
- The remark on `ITransferContext.MaxFileSize` still lists `sftp` as not reading it; the file is outside this task's `touches`, so the fix is filed as BL-1336.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. An sftp:// download is cut at --max-filesize with exit 63 and curl's 'Exceeded the maximum allowed file size (N) with N bytes' text and -v line
