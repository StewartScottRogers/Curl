---
id: BL-1241
title: Fail an SFTP download whose STAT size has its top bit set with curl's exit 36 'Bad file size'
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1241 — Fail an SFTP download whose STAT size has its top bit set with curl's exit 36 'Bad file size'

## Goal

An `sftp://` download, or an upload with `-C -` that asks the server for the remote size, whose `SSH_FXP_ATTRS` answer carries a size of 2^63 or more fails with exit 36 (`CurlExitCode.BadDownloadResume`) and curl 8.21.0's `Bad file size (<size as a signed 64-bit number>)`, after closing the open handle, instead of treating the size as unknown.

## Context

- Today `Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpSession.cs` `ReadSize` casts the 64-bit size to `long` and returns `size > 0 ? size : null`, so a size with its top bit set becomes "unknown": `SftpFileDownload.DownloadAsync` (`Sftp/SftpFileDownload.cs`) then reads until the end of the file, and `-C -` on an upload starts at 0.
- curl 8.21.0 with libssh2 1.11.1, `lib/vssh/libssh2.c` at `curl-8_21_0` (https://github.com/curl/curl/blob/curl-8_21_0/lib/vssh/libssh2.c): libssh2 stores the size as `libssh2_uint64_t` and curl reads it into a signed `curl_off_t`. `sftp_download_stat` (lines 1330-1336) and `sftp_upload_init`'s `-C -` stat (lines 940-950) both answer a negative value with `failf "Bad file size (%" FMT_OFF_T ")"` and `CURLE_BAD_DOWNLOAD_RESUME` (36); `ssh_state_sftp_download_stat` and `ssh_state_sftp_upload_init` (lines 2689-2703, 2790-2804) then go to `SSH_SFTP_CLOSE`, so the handle is closed. A size of 0, or a STAT answer without the size flag, stays "unknown" as today (lines 1316-1328, already pinned by `DownloadAsync_EmptyFile_ReadsAheadAsForAnUnknownSizeAndSucceedsAsMeasured`).
- The existing tests show the shape to copy: `Curl.Protocol.Ssh.UnitTests/Sftp/SftpFileDownloadTests.Ranges.cs` `DownloadAsync_PartBeyondTheEnd_EndsWithExit33AndClosesTheHandleAsMeasured` drives `SftpServerScript` and asserts the failure, no output and the CLOSE request. This case cannot be measured against OpenSSH's `sftp-server`, which never reports such a size; pin it from the source and say so in the test comment.

## Acceptance criteria

- [ ] A test drives a download whose STAT answer carries the size `0x8000000000000000` and asserts exit 36 `Bad file size (-9223372036854775808)`, nothing written, no READ sent and the handle closed; a second row with `0xFFFFFFFFFFFFFFFF` asserts `Bad file size (-1)`.
- [ ] A test drives an upload with `-C -` (`ResumeFrom` negative) whose STAT answer carries `0x8000000000000000` and asserts exit 36 with the same message and no data written to the server.
- [ ] Sizes of 0 and answers without the size flag keep today's behaviour; the existing tests for them pass unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
