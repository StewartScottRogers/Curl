---
id: BL-597
title: Upload a file to an SMB share
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-596]
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-597 — Upload a file to an SMB share

## Goal

`-T file smb://host/share/path` creates or truncates the remote file and writes the bytes, as curl 8.21.0 does, with a refused create or write mapped to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 39. Builds on BL-596.
- Measure against a local Samba server as in BL-595: a new file, an existing file, a read-only share, `-T -`.

## Acceptance criteria

- [x] Measured first as above; stderr, exit code and the resulting remote file copied into Notes.
- [x] `Curl.Protocol.Smb.UnitTests` pin the create and write requests and the outcome for each case; upload progress and `%{size_upload}` match the measurement.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measured 2026-09-29** as BL-595 and BL-596 did (no Samba server; the Windows Schannel
  build has no SMB): Ubuntu's curl 8.18.0 (WSL) run by `Record-CurlExchange.ps1 -Script`
  against a hand-assembled server, `-sS -m 6 -u User:Password -T <file> -w
  '[%{size_upload} %{size_download}]' smb://172.26.96.1:14450/share/dir/x.txt`. The
  "remote file" is the WRITE_ANDX data the server received. Every request byte is in
  `SmbRecordedExchange`.
  - New file (open create action FILE_CREATED; write count 11): stdout `[11 0]`, stderr
    empty, exit 0. curl opens with access `GENERIC_READ|GENERIC_WRITE` (0xc0000000) and
    disposition `FILE_OVERWRITE_IF` (5), then one WRITE_ANDX (14 words, FID, offset 0,
    data length 11, data offset 0x40, byte count 12, a pad byte, `hello world`), CLOSE,
    TREE_DISCONNECT. Remote file received: `hello world`.
  - Existing file (create action FILE_OVERWRITTEN): identical bytes and outcome; curl
    ignores the create action - FILE_OVERWRITE_IF truncates it server-side.
  - Read-only share, open refused STATUS_ACCESS_DENIED (0xc0000022): `curl: (78) Remote
    file not found`, exit 78, `[0 0]`; then TREE_DISCONNECT only. With the DOS ERRnoaccess
    status: `curl: (9) Access denied to remote resource`, exit 9.
  - Write refused (0xc0000022): `curl: (25) Upload failed (at start/before it took off)`,
    exit 25, `[0 0]`; curl still sends CLOSE and TREE_DISCONNECT.
  - `-T -` (stdin `hello world`): `curl: (55) SMB upload needs to know the size up front`,
    exit 55, after the session setup; nothing more sent, no tree connect.
  - Empty file: one WRITE_ANDX with data length 0, byte count 1; exit 0, `[0 0]`.
  - 40000-byte file: writes of 0x7fff bytes at 0 then 0x1c41 at 0x7fff; exit 0, `[40000 0]`.
  - Server says it wrote 5 of 11: curl's second write declares 6 bytes at offset 5 but
    sends none (its source is at its end), then waits until `-m`: exit 28, `[5 0]`.
  - URL ending `/` (`.../share/dir/`): curl appends the source's name (`dir\up.txt`)
    before the handler runs; that is the command line's, not this library's.
- **Choices taken** (from the measurement and `lib/smb.c`, no ADR needed): the upload
  size is the source's `Length - Position`, and a source that cannot seek is curl's
  unknown `infilesize`, so exit 55; progress and `%{size_upload}` count the bytes each
  write response says were written (the report's `UploadSize`, `DownloadSize` 0); the
  next write starts from that count, declaring the planned length even when the source
  gives fewer bytes, as curl does. `SmbFileDownloader` became `SmbFileTransfer`
  (`TransferAsync`) since it now uploads too.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports 100%
  line, 100% branch, 74 members, 0 failing, worst CRAP 8. 92 tests in
  `Curl.Protocol.Smb.UnitTests`; `dotnet build Curl.slnx -warnaserror` clean; fast tests
  green in every project except one run of `Curl.Core.UnitTests`'
  `AsWindowsStatReportsIt_LocalPipe_IsItsInstanceCount`, which counts machine-wide pipe
  instances while other lanes run and passed when rerun; this task does not touch it.
- The handler is still not registered in `Curl.Console` until BL-598.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. smb:// -T creates or truncates the remote file and writes it as curl does, with exit 78/9 for a refused open, 25 for a refused write and 55 for -T -
