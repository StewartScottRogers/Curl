---
id: BL-1285
title: Stop a telnet session under -I or past --max-filesize as curl's download writer does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: FR-084
created: 2026-10-02
completed:
---
# BL-1285 — Stop a telnet session under -I or past --max-filesize as curl's download writer does

## Goal

A `telnet://` session honours `ITransferContext.NoBody` (`-I`) and `ITransferContext.MaxFileSize` (`--max-filesize`) as curl 8.21.0 does: under `-I` the first data from the server ends the session with exit 8 and writes nothing; past the limit the allowed bytes are written and the session ends with exit 63.

## Context

- Today `Curl.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs` `ReceiveUntilClosedAsync` passes every run of received data to `WriteOutputAsync` and reads neither `context.NoBody` nor `context.MaxFileSize`.
- curl 8.21.0 writes telnet data through `Curl_client_write` (`lib/telnet.c` line 1070, the `PRINT` path of `telrcv`), so every write meets `cw_download_write` in `lib/sendf.c` (tag `curl-8_21_0`):
  - lines 214-224: with `no_body` set and no headers received, the first body bytes return `CURLE_WEIRD_SERVER_REPLY` (exit 8) without `failf`: no `-v` line, exit text curl_easy_strerror's `Weird server reply`;
  - lines 251-291: with `max_filesize` set, a write is cut to the bytes left under the limit, the cut part is written, and when bytes were cut `failf(data, "Exceeded the maximum allowed file size (%ld) with %ld bytes", ...)` returns `CURLE_FILESIZE_EXCEEDED` (exit 63). A body exactly at the limit does not fail; a limit of 0 is no limit; the count runs across writes.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response 'hello'` (the server sends `hello` and closes):
  - `-sv -I telnet://127.0.0.1:PORT`: exit 8, stdout empty, stderr ends `{ [5 bytes data]` then `* shutting down connection #0`.
  - `-sv --max-filesize 3 telnet://127.0.0.1:PORT`: exit 63, stdout `hel`, stderr ends `{ [5 bytes data]`, `* Exceeded the maximum allowed file size (3) with 3 bytes`, `* shutting down connection #0`.
  Curl today writes `hello` and exits 0 in both runs.
- `TelnetTraceReporter.ConnectionEnded` already writes a failure's message and then `shutting down connection #N` for every exit but 23; the new failures go through it. `TelnetTraceReporter.IsStrerrorText` lists the messages curl prints without `failf`; `Weird server reply` belongs with them, so exit 8 writes no message line.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Telnet.UnitTests` runs a session with `NoBody = true` whose fake connection delivers `hello` and asserts exit 8 (`CurlExitCode.WeirdServerReply`) with message `Weird server reply`, nothing written to the output, the received-data event for the 5 bytes still reported, and the info lines ending with `shutting down connection #0` and no `Weird server reply` line.
- [ ] A test runs a session with `MaxFileSize = 3` delivering `hello` and asserts exit 63 (`CurlExitCode.FilesizeExceeded`) with message `Exceeded the maximum allowed file size (3) with 3 bytes`, output `hel`, and the info lines ending with that message then `shutting down connection #0`.
- [ ] A test delivering `he` then `llo` in two reads with `MaxFileSize = 4` asserts output `hell` and the message `... (4) with 4 bytes`, pinning that the limit counts across writes.
- [ ] Tests pin that `MaxFileSize` of 0, `null`, and exactly 5 all end with exit 0 and output `hello`; negotiation commands (`IAC DO ...`) in the received bytes are not counted against the limit.
- [ ] `dotnet build Curl.Protocol.Telnet.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Telnet.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-02: Created.
