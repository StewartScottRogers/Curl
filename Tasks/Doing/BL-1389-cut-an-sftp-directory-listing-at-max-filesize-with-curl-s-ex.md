---
id: BL-1389
title: Cut an SFTP directory listing at --max-filesize with curl's exit 63 'Exceeded the maximum allowed file size' text
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: FR-084
created: 2026-10-03
completed:
---
# BL-1389 — Cut an SFTP directory listing at --max-filesize with curl's exit 63 'Exceeded the maximum allowed file size' text

## Goal

An `sftp://host/dir/` listing, long form or `-l`, honours `ITransferContext.MaxFileSize` as curl 8.21.0's download writer does: listing bytes are written up to the limit, the write that passes it is cut there, and the transfer fails with exit 63 and `Exceeded the maximum allowed file size (N) with N bytes`.

## Context

- Today `Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDirectoryListing.cs` (`Listing.ListUntilEndAsync`) writes every line in full and never reads `MaxFileSize`; BL-1327 (SFTP file download) and BL-1328 (SCP) added the cut to downloads through `Curl.Protocol.Ssh.UnitLibrary/DownloadSizeLimit.cs` (`AllowedOf`, `Exceeded`), and BL-1327's Notes left the listing out on purpose.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/vssh/libssh2.c`: `sftp_readdir` (lines 1398-1440) writes a `-l` name and then `"\n"` with `Curl_client_write(data, CLIENTWRITE_BODY, ...)`; `ssh_state_sftp_readdir_bottom` (lines 2751-2773) writes each long-form line (with its `\n`) the same way. Every body write goes through `cw_download_write` in `lib/sendf.c` (lines 251-257 cut a write to the bytes left under `max_filesize`; lines 286-291 then `failf(... "Exceeded the maximum allowed file size (%" FMT_OFF_T ") with %" FMT_OFF_T " bytes" ...)` and return `CURLE_FILESIZE_EXCEEDED`). A listing exactly at the limit succeeds; a limit of 0 is no limit.
- On the cut, `ssh_state_sftp_readdir_bottom` moves to `SSH_STOP` and `sftp_readdir`'s caller fails the state, so the directory handle is closed by the usual failure teardown; pin whatever `SftpDirectoryListing` sends today for any other failure mid-listing (as BL-1327 pinned for downloads in `SshProtocolHandlerTests.FailedSftpTeardown.cs`).

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Ssh.UnitTests` lists a fake SFTP directory whose long-form output is longer than 10 bytes with `MaxFileSize = 10` and asserts exit 63 (`CurlExitCode.FilesizeExceeded`), message `Exceeded the maximum allowed file size (10) with 10 bytes`, and exactly the first 10 bytes of the listing written, cut inside a line.
- [ ] The same with `-l` (list only) cuts the names-and-newlines output at 10 bytes with the same exit and message.
- [ ] Tests pin that `MaxFileSize` of 0, `null`, and exactly the listing's length complete with exit 0 and the whole listing.
- [ ] A test pins the packets sent after the cut (close of the directory handle and the session teardown) as the existing failed-listing path sends them.
- [ ] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports no failing member in the code this task changed.

## Notes

- Reuse `DownloadSizeLimit` rather than a second copy of the cut.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
