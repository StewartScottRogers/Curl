---
id: BL-1118
title: Match curl's FTP 552 exit 70 and its -C resume -v lines
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1117]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1118 — Match curl's FTP 552 exit 70 and its -C resume -v lines

## Goal

An FTP end-of-transfer reply of `552` ends with exit 70 `Exceeded storage allocation` (today exit 18), and the three `-v` lines curl 8.21.0 prints around `-C` are reported: `File already completely downloaded`, `File already completely uploaded` and `ftp server does not support SIZE`.

## Context

- curl 8.21.0 `lib/ftp.c` (https://github.com/curl/curl/blob/curl-8_21_0/lib/ftp.c):
  - `ftp_done`, around line 3745: the end-of-transfer reply `226`/`250` is fine, `552` is `failf(data, "Exceeded storage allocation"); result = CURLE_REMOTE_DISK_FULL;` (exit 70), anything else `server did not report OK, got %d` (exit 18).
  - `ftp_state_retr`, lines 1791-1840: only with a `-C` offset, a failed `SIZE` (`filesize == -1`) is `infof(data, "ftp server does not support SIZE")` and the download goes on; when the offset leaves nothing to fetch, `infof(data, "File already completely downloaded")` and the transfer stops with exit 0 and no `RETR`.
  - `ftp_state_ul_setup`, line 1754: when the `-C` offset covers the whole upload of known size, `infof(data, "File already completely uploaded")` and nothing is stored.
- Curl today, `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`: `ReadTransferCompleteAsync` fails every code but 226 and 250 with `CurlExitCode.PartialFile` and `FtpTransferMessages.TransferNotOk`; `PositionWithinSizeAsync` ends a zero-remaining `-C` download through `EndAndSucceedAsync` without a line; `PositionAsync` goes to `RestartAtAsync` when `fileSize` is null without a line; the upload path (around line 672, `FtpUploadOffset.TrySkip` returning `false`) goes to `QuitAndSucceedAsync` without a line. `CurlExitCode.RemoteDiskFull` (70) exists.
- Whether curl sends `QUIT` after the 552: follow the existing exit-18 path in `ReadTransferCompleteAsync` (which sends it), since `ftp_done` keeps the control connection valid for both; confirm with `Record-CurlExchange.ps1` if the session's script can be made to answer `STOR` with 552, and record what was measured in Notes.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Ftp.UnitTests` pins an upload whose end-of-transfer reply is `552 Quota exceeded` returning exit 70 `Exceeded storage allocation`; the existing tests for other non-226/250 codes still return exit 18.
- [ ] Tests pin the `-v` info line `File already completely downloaded` for `-C` equal to the `SIZE` count (no `RETR` sent), `ftp server does not support SIZE` for `-C` with `SIZE` answered `550` (the download goes on with `REST`), and `File already completely uploaded` for an upload whose `-C` offset covers the source (no `STOR` or `APPE` sent).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
