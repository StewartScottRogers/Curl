---
id: BL-1405
title: Refuse a WebSocket upgrade reply header with no colon, a NUL byte, a carriage return or a second different Location with curl's exit 8
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1404]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: FR-078
created: 2026-10-03
completed:
---
# BL-1405 — Refuse a WebSocket upgrade reply header with no colon, a NUL byte, a carriage return or a second different Location with curl's exit 8

## Goal

A `ws://` or `wss://` upgrade reply whose head carries a header line curl 8.21.0's shared HTTP header code refuses ends the transfer with exit 8 and curl's message, the refused line not written: `Header without colon`, `Nul byte in header`, `Carriage return found in header`, or `Multiple Location headers` for a second `Location` that differs from the first.

## Context

- Today `Curl.Protocol.Ws.UnitLibrary/WsUpgradeResponseReader.cs` (`ReadAsync`) checks only for an empty reply, HTTP/0.9 and a head over 102400 bytes; none of the four texts exists in the WebSocket library, so such a reply is read as an ordinary refused or accepted upgrade. The HTTP library refuses them (`Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs` lines 43-60: `HeaderWithoutColon`, `NulByteInHeader`, `MultipleLocationHeaders`, `CarriageReturnInHeader`; BL-1331), but protocol libraries cannot share code.
- curl 8.21.0 (tag `curl-8_21_0`): a WebSocket upgrade reply is parsed by the HTTP response code in `lib/http.c` (`http_rw_hd` and `Curl_http_header`), which refuses these header lines with `CURLE_WEIRD_SERVER_REPLY` before writing them to the trace. Read the exact checks and their order from that file (the same ones BL-1331 followed for HTTP).
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1`, `-sv ws://127.0.0.1:PORT/a`, each reply a `200` ending `Content-Length: 0\r\n\r\n` after the line shown:
  - `BadHeader\r\n`: `< HTTP/1.1 200 OK`, `* Header without colon`, `* closing connection #0`; exit 8.
  - `X-A: a\0b\r\n`: `* Nul byte in header`, `* closing connection #0`; exit 8.
  - `X-A: a\rb\r\n`: `* Carriage return found in header`, `* closing connection #0`; exit 8.
  - a `302` with `Location: /x\r\nLocation: /y\r\n`: `< Location: /x`, `* Multiple Location headers`, `* closing connection #0`; exit 8.
- BL-1404 adds the `Content-Length` checks to the same reader; build on it.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Ws.UnitTests` drive the handler with each measured reply and pin the `-v` lines, exit 8 (`CurlExitCode.WeirdServerReply`) and the message, with the refused line not written.
- [ ] Tests pin that two equal `Location` headers are accepted, and that a `101` reply with a header line lacking a colon is refused the same way (the checks apply to every head).
- [ ] `dotnet build Curl.Protocol.Ws.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Ws.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports no failing member in the code this task changed.

## Notes

- Measured too: a `101` reply `HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nBadHeader\r\nConnection: Upgrade\r\n\r\n` writes `< Upgrade: websocket`, `* Header without colon`, `* closing connection #0`; exit 8.

## Log

- 2026-10-03: Created.
