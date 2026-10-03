---
id: BL-1294
title: Cut a WebSocket transfer at --max-filesize counted in frame bytes, with exit 63
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: FR-084
created: 2026-10-02
completed:
---
# BL-1294 — Cut a WebSocket transfer at --max-filesize counted in frame bytes, with exit 63

## Goal

A `ws://` or `wss://` transfer honours `ITransferContext.MaxFileSize` as curl 8.21.0 does: the limit counts the raw frame bytes received after the 101 head, only the frame bytes under the limit are decoded and their payload written, and the transfer ends with exit 63, `Exceeded the maximum allowed file size (N) with N bytes`, closing the connection.

## Context

- Today `Curl.Protocol.Ws.UnitLibrary/WsProtocolHandler.cs` `ExchangeFramesAsync` hands every received byte to `WsFrameReceiver` (`WsFrameReceiver.cs`, `DeliverAlreadyReceivedAsync` and `ReceiveUntilClosedAsync`), which decodes frames and writes their payload through `writePayload`; nothing reads `context.MaxFileSize`.
- curl 8.21.0 (tag `curl-8_21_0`): after the 101, received bytes go down the client writer chain, where the download writer `cw_download_write` (`lib/sendf.c`, created in phase `CURL_CW_PROTOCOL`, line 350) runs before the WebSocket decoder (`lib/ws.c` line 1385, phase `CURL_CW_CONTENT_DECODE`). So `cw_download_write` lines 251-291 cut each write of raw frame bytes to what is left under the limit, pass only that prefix on to the decoder, and when bytes were cut `failf(data, "Exceeded the maximum allowed file size (%ld) with %ld bytes", max_filesize, bytecount)` returns `CURLE_FILESIZE_EXCEEDED` (exit 63). A stream exactly at the limit does not fail; 0 is no limit.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response 'HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: xxx\r\n\r\n\x81\x05hello\x88\x00'` and `-sv --max-filesize 3 ws://127.0.0.1:PORT/`: exit 63, stdout `h` (the 3 raw bytes `81 05 68` hold one payload byte); stderr ends `* [WS] Received 101, switch to WebSocket`, `{ [9 bytes data]`, `* Exceeded the maximum allowed file size (3) with 3 bytes`, `* closing connection #0`. Curl today writes `hello`, answers the close frame and ends `* shutting down connection #0` with exit 0.
- The received-data event still carries all 9 bytes; no CLOSE frame is sent back after the cut.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Ws.UnitTests` drives the measured exchange with `MaxFileSize = 3` and asserts exit 63 (`CurlExitCode.FilesizeExceeded`), message `Exceeded the maximum allowed file size (3) with 3 bytes`, output `h`, a 9-byte received-data event, nothing sent after the upgrade request, and the connection ending `closing connection #0`.
- [ ] A test with `MaxFileSize = 7` (exactly the text frame) asserts output `hello`, then the CLOSE frame's first byte over the limit ends the transfer with `... (7) with 7 bytes`.
- [ ] A test where the frame bytes arrive together with the 101 head (the reader's already-received bytes) pins that those count against the limit too.
- [ ] Tests pin that `MaxFileSize` of 0, `null` and exactly 9 end as today with output `hello`.
- [ ] `dotnet build Curl.Protocol.Ws.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Ws.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-02: Created.
