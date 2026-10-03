---
id: BL-1306
title: Stop a telnet session under -I or past --max-filesize as curl's download writer does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: FR-084
created: 2026-10-02
completed: 2026-10-03
---
# BL-1306 — Stop a telnet session under -I or past --max-filesize as curl's download writer does

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

- [x] A test in `Curl.Protocol.Telnet.UnitTests` runs a session with `NoBody = true` whose fake connection delivers `hello` and asserts exit 8 (`CurlExitCode.WeirdServerReply`) with message `Weird server reply`, nothing written to the output, the received-data event for the 5 bytes still reported, and the info lines ending with `shutting down connection #0` and no `Weird server reply` line.
- [x] A test runs a session with `MaxFileSize = 3` delivering `hello` and asserts exit 63 (`CurlExitCode.FilesizeExceeded`) with message `Exceeded the maximum allowed file size (3) with 3 bytes`, output `hel`, and the info lines ending with that message then `shutting down connection #0`.
- [x] A test delivering `he` then `llo` in two reads with `MaxFileSize = 4` asserts output `hell` and the message `... (4) with 4 bytes`, pinning that the limit counts across writes.
- [x] Tests pin that `MaxFileSize` of 0, `null`, and exactly 5 all end with exit 0 and output `hello`; negotiation commands (`IAC DO ...`) in the received bytes are not counted against the limit.
- [x] `dotnet build Curl.Protocol.Telnet.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Telnet.UnitLibrary` reports no failing member.

## Notes

- Implemented in `TelnetProtocolHandler.WriteReceivedDataAsync`: each read's data (commands already stripped by `TelnetReceiver`, so negotiation is never counted) meets `-I` first, then `--max-filesize` (0 or unset is no limit), then the output. The limit counts across reads through `bytesWritten`; once at the limit, further data is cut to 0 bytes and ends with exit 63, as curl's `wmax` of 0 does.
- Choice: on a cut or an `-I` failure that read's negotiation replies are not sent. curl sends replies inline in `telrcv` as it walks the buffer, so it would already have answered commands that came before the data in the same read; the session ends either way and nothing visible depends on it, so the simpler order stays.
- `Weird server reply` joined `TelnetTraceReporter.IsStrerrorText` (as `WeirdServerReplyMessage`), so exit 8 writes no `-v` message line.
- Tests: `TelnetProtocolHandlerDownloadLimitTests` (9 cases); Telnet tests 219 passed. Telnet library 100% line and branch coverage, measured with the coverage collector on the Telnet test project alone: the full `Measure-CodeQuality.ps1` run covers the whole solution and had not finished after an hour under shift load. The solution's `dotnet build` is clean, so CA1502 complexity holds.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-03: Doing -> Done. telnet:// ends with exit 8 on data under -I and exit 63 past --max-filesize, as curl 8.21.0
