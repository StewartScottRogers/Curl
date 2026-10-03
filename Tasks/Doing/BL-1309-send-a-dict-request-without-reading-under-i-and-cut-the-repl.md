---
id: BL-1309
title: Send a dict request without reading under -I, and cut the reply at --max-filesize with exit 63
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests]
requirement: FR-084
created: 2026-10-02
completed:
---
# BL-1309 — Send a dict request without reading under -I, and cut the reply at --max-filesize with exit 63

## Goal

A `dict://` transfer honours `ITransferContext.NoBody` (`-I`) and `ITransferContext.MaxFileSize` (`--max-filesize`) as curl 8.21.0 does: under `-I` it sends its whole request and ends with exit 0 without reading the reply; past the limit it writes the allowed bytes and ends with exit 63.

## Context

- Today `Curl.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs` copies every byte the server sends to `context.Output` (the read loop around line 184) and reads neither `context.NoBody` nor `context.MaxFileSize`.
- curl 8.21.0 (tag `curl-8_21_0`):
  - `lib/dict.c` lines 201, 242 and 264: each request kind ends with `Curl_xfer_setup_recv(data, FIRSTSOCKET, -1)`; `lib/transfer.c` lines 706-711: a protocol with no response-header writer (dict has none) does not receive at all when `no_body` is set, so the transfer ends at once with exit 0.
  - `lib/sendf.c` lines 251-291 (`cw_download_write`): with `max_filesize` set, a body write is cut to the bytes left under the limit, the cut part is written, and when bytes were cut `failf(data, "Exceeded the maximum allowed file size (%ld) with %ld bytes", ...)` returns `CURLE_FILESIZE_EXCEEDED` (exit 63). A body exactly at the limit does not fail; 0 is no limit; the count runs across reads.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response '220 hi\r\n150 1 found\r\n151 "hello" wn\r\nhi\r\n.\r\n250 ok\r\n'`:
  - `-sv -I dict://127.0.0.1:PORT/d:hello`: exit 0, stdout empty, `request.bin` the usual `CLIENT libcurl 8.21.0\r\nDEFINE ! hello\r\nQUIT\r\n`; stderr `Trying`, `Established`, `} [45 bytes data]`, `* shutting down connection #0`.
  - `-sv --max-filesize 3 dict://127.0.0.1:PORT/d:hello`: exit 63, stdout `220`; stderr `Trying`, `Established`, `} [45 bytes data]`, `* Exceeded the maximum allowed file size (3) with 3 bytes`, `* closing connection #0`.
  Curl today writes the whole reply and exits 0 in both runs (its `-v` lines otherwise match, including `shutting down` for a success).

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Dict.UnitTests` runs a `d:hello` transfer with `NoBody = true` and asserts exit 0, the full request written, no read made afterwards (the fake connection records reads), nothing written to the output, and the info lines ending `shutting down connection #0`.
- [ ] A test with `MaxFileSize = 3` and the measured reply asserts exit 63 (`CurlExitCode.FilesizeExceeded`), message `Exceeded the maximum allowed file size (3) with 3 bytes`, output `220`, and the info lines ending with that message then `closing connection #0`.
- [ ] A test delivering the reply in two reads with a limit that falls inside the second asserts the first read written whole and the second cut, with the message's two numbers both the limit.
- [ ] Tests pin that `MaxFileSize` of 0, `null` and exactly the reply's length end with exit 0 and the whole reply.
- [ ] `dotnet build Curl.Protocol.Dict.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Dict.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Dict.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
