---
id: BL-1308
title: Send a gopher selector without reading under -I, and cut the reply at --max-filesize with exit 63
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests]
requirement: FR-084
created: 2026-10-02
completed:
---
# BL-1308 — Send a gopher selector without reading under -I, and cut the reply at --max-filesize with exit 63

## Goal

A `gopher://` transfer honours `ITransferContext.NoBody` (`-I`) and `ITransferContext.MaxFileSize` (`--max-filesize`) as curl 8.21.0 does: under `-I` it sends the selector and ends with exit 0 without reading the reply; past the limit it writes the allowed bytes and ends with exit 63.

## Context

- Today `Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs` `CopyReplyAsync` copies the whole reply to `context.Output` and reads neither `context.NoBody` nor `context.MaxFileSize`.
- curl 8.21.0 (tag `curl-8_21_0`):
  - `lib/gopher.c` line 165: after sending the selector, `Curl_xfer_setup_recv(data, FIRSTSOCKET, -1)`; `lib/transfer.c` lines 706-711: a protocol with no response-header writer (gopher has none) does not receive at all when `no_body` is set, so the transfer ends at once with exit 0.
  - `lib/sendf.c` lines 251-291 (`cw_download_write`): with `max_filesize` set, a body write is cut to the bytes left under the limit, the cut part is written, and when bytes were cut `failf(data, "Exceeded the maximum allowed file size (%ld) with %ld bytes", ...)` returns `CURLE_FILESIZE_EXCEEDED` (exit 63). A body exactly at the limit does not fail; 0 is no limit; the count runs across reads.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response 'hello\r\n.\r\n'`:
  - `-sv -I gopher://127.0.0.1:PORT/0/x`: exit 0, stdout empty, `request.bin` is `/x\r\n`; stderr is the `Trying`/`Established` lines then `* shutting down connection #0`, with no `{ [N bytes data]` line.
  - `-sv --max-filesize 3 gopher://127.0.0.1:PORT/0/x`: exit 63, stdout `hel`; stderr ends `{ [10 bytes data]`, `* Exceeded the maximum allowed file size (3) with 3 bytes`, `* closing connection #0`.
  Curl today writes `hello\r\n.\r\n` and exits 0 in both runs.
- `GopherProtocolHandler`'s connection-end lines already write a failure's message and then `closing connection #N` for a failure other than a malformed selector; exit 63 goes through that path.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Gopher.UnitTests` runs a transfer with `NoBody = true` and asserts exit 0, the selector `/x\r\n` written to the fake connection, no read made after it (the fake records reads), nothing written to the output, no received-data event, and the info lines ending `shutting down connection #0`.
- [ ] A test with `MaxFileSize = 3` and a reply of `hello\r\n.\r\n` asserts exit 63 (`CurlExitCode.FilesizeExceeded`), message `Exceeded the maximum allowed file size (3) with 3 bytes`, output `hel`, a received-data event of all 10 bytes, and the info lines ending with that message then `closing connection #0`.
- [ ] A test delivering the reply in two reads (`he`, `llo`) with `MaxFileSize = 4` asserts output `hell` and the message `... (4) with 4 bytes`.
- [ ] Tests pin that `MaxFileSize` of 0, `null` and exactly the reply's length end with exit 0 and the whole reply.
- [ ] `dotnet build Curl.Protocol.Gopher.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Gopher.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Gopher.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
