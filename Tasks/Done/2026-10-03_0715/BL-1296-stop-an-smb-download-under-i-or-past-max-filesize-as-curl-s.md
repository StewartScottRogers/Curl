---
id: BL-1296
title: Stop an SMB download under -I or past --max-filesize as curl's download writer does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: FR-084
created: 2026-10-02
completed: 2026-10-03
---
# BL-1296 — Stop an SMB download under -I or past --max-filesize as curl's download writer does

## Goal

An `smb://`/`smbs://` download honours `ITransferContext.NoBody` (`-I`) and `ITransferContext.MaxFileSize` (`--max-filesize`) as curl 8.21.0 does: under `-I` the first READ data ends the download with exit 8 and writes nothing; past the limit the allowed bytes are written and the download ends with exit 63; either way the file is closed and the tree disconnected before the result is returned.

## Context

- Today `Curl.Protocol.Smb.UnitLibrary/SmbFileTransfer.cs` writes each READ reply's data to `context.Output` (`WriteAsync`, around line 203) after reporting it as received data (`ReportDataReceived`); nothing reads `context.NoBody` or `context.MaxFileSize`. An output write failure (exit 23) already ends through `CloseAsync` and `DisconnectAsync`.
- curl 8.21.0 (tag `curl-8_21_0`):
  - `lib/smb.c` lines 1095-1112: each READ_ANDX reply's data goes to `Curl_client_write(data, CLIENTWRITE_BODY, ...)`; a failure is kept in `req->result` and the state machine moves to `SMB_CLOSE`, then `SMB_TREE_DISCONNECT`, then `SMB_DONE` returns `req->result` (lines 1165-1175).
  - `lib/sendf.c` `cw_download_write`, lines 214-224: with `no_body` set and no headers received, the first body bytes return `CURLE_WEIRD_SERVER_REPLY` (exit 8) without `failf` (no `-v` line; exit text `Weird server reply`); lines 251-291: with `max_filesize` set, a write is cut to the bytes left under the limit, the cut part is written, and when bytes were cut `failf(data, "Exceeded the maximum allowed file size (%ld) with %ld bytes", ...)` returns `CURLE_FILESIZE_EXCEEDED` (exit 63). A file exactly at the limit does not fail; 0 is no limit; the count runs across READ replies.
  - `smb.c` line 1082 sets the download size from the OPEN reply, but that is progress only; no size check happens before the first READ.
- Comparison: the curl 8.21.0 source above, not measured. `Curl.Protocol.Smb.UnitTests/SmbRecordedExchange.cs` holds the recorded exchange the existing tests replay; `Record-CurlExchange.ps1 -Script` can serve the same frames to real curl if a measurement is wanted, and its bytes then go into the tests.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Smb.UnitTests` replays a download with `NoBody = true` and asserts exit 8 (`CurlExitCode.WeirdServerReply`), message `Weird server reply`, nothing written to the output, the READ data still reported as received data, no info line for the failure, and that CLOSE and TREE_DISCONNECT are sent before the transfer returns.
- [x] A test with `MaxFileSize = 3` and a longer file asserts exit 63 (`CurlExitCode.FilesizeExceeded`), message `Exceeded the maximum allowed file size (3) with 3 bytes`, the first 3 bytes written, no further READ sent, and CLOSE and TREE_DISCONNECT sent.
- [x] A test with a file read in two READ replies and a limit inside the second pins that the first is written whole and the second cut.
- [x] Tests pin that `MaxFileSize` of 0, `null` and exactly the file's size end with exit 0 and the whole file; an upload (`-T`) ignores both settings.
- [x] `dotnet build Curl.Protocol.Smb.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Smb.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports no failing member.

## Notes
- 2026-10-03, interactive session: timed out after 120 min because `Measure-CodeQuality.ps1 -Library` ran the whole solution's ~25k tests with coverage (40-58 min a run on nine lanes) and other lanes killed runs machine-wide; both fixed (BL-1318). All four test criteria were written and green (138/138); only the Measure check was never completed. Restore, rebuild, test, then `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary`. The work is in the shared stash `65f0ded78d580000ebe016d63f7f5514d7a9bbc7` (never pop a stash; lanes share them). Restore it with `git checkout 65f0ded78d580000ebe016d63f7f5514d7a9bbc7 -- Curl.Protocol.Smb.UnitLibrary/SmbFileTransfer.cs Curl.Protocol.Smb.UnitLibrary/SmbMessages.cs Curl.Protocol.Smb.UnitTests/SmbFileTransferTests.cs Curl.Protocol.Smb.UnitTests/SmbProtocolHandlerVerboseTests.cs` and continue from there.

- 2026-10-03, lane 3: restored the stashed work (tracked files from `65f0ded7`, the untracked `SmbFileTransferTests.BodyLimits.cs` from its third parent `dde2a698`). Build clean with -warnaserror, 138/138 SMB tests pass, `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary`: line 100%, branch 100%, 0 failing members, worst CRAP 10. Behaviour follows the curl 8.21.0 source cited above (not measured); no new design choice, so no ADR.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-03: Doing -> Blocked. Stewart: dark factory timed out after 120 min; see Z:\repos\Curl.logs\BL-1296-20261002-211047-L4.jsonl
- 2026-10-03: Blocked -> Backlog. Requeued: the timeout was Measure-CodeQuality running the whole solution (fixed in BL-1318); Notes say how to restore the stashed work
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. SMB downloads stop under -I (exit 8) and past --max-filesize (exit 63) as curl's download writer does, closing and disconnecting first
