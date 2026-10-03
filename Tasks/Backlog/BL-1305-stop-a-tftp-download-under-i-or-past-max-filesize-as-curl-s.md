---
id: BL-1305
title: Stop a TFTP download under -I or past --max-filesize as curl's download writer does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1304]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: FR-084
created: 2026-10-02
completed:
---
# BL-1305 — Stop a TFTP download under -I or past --max-filesize as curl's download writer does

## Goal

A `tftp://` download honours `ITransferContext.NoBody` (`-I`) and `ITransferContext.MaxFileSize` (`--max-filesize`) as curl 8.21.0 does: under `-I` the first DATA block ends the transfer with exit 8 and writes nothing; past the limit the allowed bytes are written and the transfer ends with exit 63; either way a 4-byte ERROR packet is sent to the server.

## Context

- Today `Curl.Protocol.Tftp.UnitLibrary/TftpDownload.cs` `AcceptDataAsync` writes every expected DATA block to `context.Output` and reads neither `context.NoBody` nor `context.MaxFileSize`; `ITransferContext.MaxFileSize`'s doc comment (`Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`) says only the `file://` and `http` handlers enforce it.
- curl 8.21.0 hands every body write of every protocol to its download writer, `cw_download_write` in `lib/sendf.c` (tag `curl-8_21_0`):
  - lines 214-224: with `no_body` set and no headers received, the first body bytes close the connection ("ignoring body") and return `CURLE_WEIRD_SERVER_REPLY` (exit 8) without `failf`, so there is no `-v` line and the exit text is curl_easy_strerror's `Weird server reply`;
  - lines 251-291: with `max_filesize` set, a write is cut to the bytes left under the limit, the cut part is written, and when bytes were cut `failf(data, "Exceeded the maximum allowed file size (%ld) with %ld bytes", max_filesize, bytecount)` returns `CURLE_FILESIZE_EXCEEDED` (exit 63). A body exactly at the limit does not fail (`nwrite < nbytes` is false). A limit of 0 is no limit.
- `lib/tftp.c` lines 1081-1091 (`tftp_receive_packet`): when `Curl_client_write` fails, curl runs `TFTP_EVENT_ERROR`, which in `tftp_rx` (lines 617-623) sends a 4-byte ERROR packet whose error-code field is the last block acknowledged (`setpacketblock(&state->spacket, state->block)`), with no message and no NUL, then ends.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Tftp` (default `TftpData` `hello\n`):
  - `-sv -I tftp://127.0.0.1:PORT/file`: exit 8, stdout empty; stderr ends `{ [6 bytes data]` then `* shutting down connection #0`; `datagrams.txt` ends `> 00040000 ACK 0`, `< ...DATA 1 6 bytes`, `> 00050000 ERROR 0`.
  - `-sv --max-filesize 3 tftp://127.0.0.1:PORT/file`: exit 63, stdout `hel`; stderr ends `{ [6 bytes data]`, `* Exceeded the maximum allowed file size (3) with 3 bytes`, `* shutting down connection #0`; the same `> 00050000 ERROR 0`.
  Curl today writes `hello\n` and exits 0 in both runs, acknowledging block 1.
- `--max-filesize` counts body bytes across blocks (`bytecount`), so a limit inside a later block writes the earlier blocks whole.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Tftp.UnitTests` runs a download with `NoBody = true` through the fake datagram channel and asserts exit 8 (`CurlExitCode.WeirdServerReply`) with message `Weird server reply`, nothing written to the output, the block's 6 payload bytes still reported as received data (curl's `{ [6 bytes data]`), no info line for the failure, and that the last datagram sent is exactly `00 05 00 00`.
- [ ] A test runs a download of `hello\n` with `MaxFileSize = 3` and asserts exit 63 (`CurlExitCode.FilesizeExceeded`) with message `Exceeded the maximum allowed file size (3) with 3 bytes`, output `hel`, all 6 payload bytes reported as received data, that message reported as an info line, and the ERROR packet `00 05 00 00` as the last datagram sent.
- [ ] A test with a 512-byte block size and a 700-byte file and `MaxFileSize = 600` asserts the first block is written whole, 88 bytes of the second, the message `... (600) with 600 bytes`, and an ERROR packet whose code field is 1 (the last block acknowledged).
- [ ] Tests pin that `MaxFileSize` of 0, `null`, and exactly the file's length all complete with exit 0 and the whole file.
- [ ] An upload (`-T`) ignores both settings: a test asserts an upload with `NoBody = true` and `MaxFileSize = 1` completes as today.
- [ ] `dotnet build Curl.Protocol.Tftp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports no failing member.

## Notes

- The `ITransferContext.MaxFileSize` doc comment in `Curl.Protocol.Abstractions.UnitLibrary` still says only `file://` and HTTP enforce it; that file is outside this task's `touches`, so leave it for the documentation pass.

## Log

- 2026-10-02: Created.
