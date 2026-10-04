---
id: BL-1328
title: Cut an SCP download at --max-filesize with curl's exit 63 'Exceeded the maximum allowed file size' text
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1327]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: FR-084
created: 2026-10-03
completed: 2026-10-03
---
# BL-1328 — Cut an SCP download at --max-filesize with curl's exit 63 'Exceeded the maximum allowed file size' text

## Goal

An `scp://` download honours `ITransferContext.MaxFileSize` exactly as BL-1327 makes the SFTP download do: the bytes under the limit are written, the rest cut, exit 63 with `Exceeded the maximum allowed file size (N) with N bytes`, also reported as a `-v` info line.

## Context

- Today `Curl.Protocol.Ssh.UnitLibrary/Scp/ScpFileDownload.cs` (`Copy`, the write at line 154) writes every byte read from the channel and never reads `context.MaxFileSize`.
- curl 8.21.0: every body write goes through `cw_download_write`, `lib/sendf.c` lines 251-291 (tag `curl-8_21_0`), which cuts the write at the limit, writes the cut part and fails with `CURLE_FILESIZE_EXCEEDED` and the text above; a body exactly at the limit does not fail; 0 is no limit. BL-1327's Context has the full reading.
- Reuse the writer BL-1327 adds rather than writing a second one. After the failure the SCP channel is closed as on any other failed SCP download today; pin what the session sends.
- An SCP upload ignores the limit.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Ssh.UnitTests` downloads `hello\n` over the fake SCP server with `MaxFileSize = 3` and asserts exit 63 (`CurlExitCode.FilesizeExceeded`), message `Exceeded the maximum allowed file size (3) with 3 bytes`, output `hel`, and the message reported as an info line.
- [x] A test with a limit inside a later channel read asserts the earlier reads are written whole.
- [x] Tests pin that `MaxFileSize` of 0, `null`, and exactly the file's length complete with exit 0, and that an SCP upload with `MaxFileSize = 1` completes as today.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports no failing member.

## Notes

- BL-1327 wrote its cut inline in `SftpFileDownload.Copy` rather than as a separate
  writer. To reuse it, as Context asks, it moved into `DownloadSizeLimit` at the library's
  root (`AllowedOf`, `Exceeded`); `SftpFileDownload` and `ScpFileDownload` both use it, and
  the SFTP tests still pass unchanged.
- `ScpFileDownload.DownloadAsync` takes `maxFileSize` as an optional last parameter, as
  `SftpFileDownload` does; the handler passes `context.MaxFileSize`. After the cut the
  channel closes as it does after any SCP download (EOF, close, then `DISCONNECT`), which
  `ExecuteAsync_ScpDownloadOverTheMaxFileSize_IsExit63WithTheInfoLineAndClosesTheChannel`
  pins. The `-v` line comes from the handler's existing `ReportReturnedFailure`.
- Tests: `Scp/ScpFileDownloadTests.MaxFileSize.cs` (cut, cut inside the second read,
  0 / null / exact / over) and `SshProtocolHandlerTests.ScpMaxFileSize.cs` (end to end,
  upload ignores it). The fast SSH suite passes, 1641 tests.
- Measure-CodeQuality: 100% line coverage, 99.8% branch coverage. The 2 failing members it
  reports are pre-existing branch gaps in `SshProtocolHandler..ctor` and
  `HandshakeAndTransferAsync`. This task does not touch either, and they are already filed
  as BL-1357. Every member this task added or changed passes.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. An scp:// download stops at --max-filesize with exit 63 and curl's text, reported as a -v line
