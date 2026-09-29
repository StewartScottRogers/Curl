---
id: BL-584
title: Write curl's -v and --trace lines for a WebSocket transfer
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-583]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-584 — Write curl's -v and --trace lines for a WebSocket transfer

## Goal

`-v`, `-i` and `--trace`/`--trace-ascii` on a `ws://` or `wss://` transfer write what curl 8.21.0 writes (the upgrade request and `101` head lines, the frame lines curl logs, closing lines), byte for byte apart from values that vary.

## Context

- Conformance audit 2026-09-28, row 36. Events: `ITransferEvents` (ADR-0046); formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TraceTransferEventWriter.cs`; the HTTP handler's head reporting is the model for the upgrade exchange.
- Measure with `Record-CurlExchange.ps1`: `-v` for a text message then close, `-i` for the same, and `--trace-ascii -`.

## Acceptance criteria

- [x] Measured first as above; stdout, stderr and trace output copied into Notes with varying parts (key, accept, ports) marked.
- [x] `Curl.Console.UnitTests` pin the measured `-v` and `-i` output and the `--trace-ascii` dump for a fixed key.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-09-28, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Port 47932`, reply
  `HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: x\r\n\r\n`
  then `81 05 "hello"` `88 02 03 e8`, all sent in one write. Varying parts: `<key>` (random
  `Sec-WebSocket-Key`), `<lport>` (curl's local port). `-sv ws://127.0.0.1:47932/p`, exit 0,
  stdout `hello` `03 e8`, stderr (CRLF after info lines, CR CR LF after header lines):
  ```
  *   Trying 127.0.0.1:47932...
  * Established connection to 127.0.0.1 (127.0.0.1 port 47932) from 127.0.0.1 port <lport> 
  * using HTTP/1.x
  > GET /p HTTP/1.1
  > Host: 127.0.0.1:47932
  > User-Agent: curl/8.21.0
  > Accept: */*
  > Upgrade: websocket
  > Sec-WebSocket-Version: 13
  > Sec-WebSocket-Key: <key>
  > Connection: Upgrade
  > 
  * Request completely sent off
  < HTTP/1.1 101 Switching Protocols
  < Upgrade: websocket
  < Connection: Upgrade
  < Sec-WebSocket-Accept: x
  < 
  * Received 101, Switching to WebSocket
  * [WS] Received 101, switch to WebSocket
  { [11 bytes data]
  * shutting down connection #0
  ```
- `-si`: stdout `hello` `03 e8` only (the head is not written, BL-583), stderr empty, exit 0.
- `--trace-ascii -` (stdout, LF only; progress meter on stderr): `=> Send header, 193 bytes (0xc1)`
  with the request one line per CRLF (`0000: GET /p HTTP/1.1` … `007d: Sec-WebSocket-Key: <key>`,
  `00aa: Connection: Upgrade`, `00bf: `), `* Request completely sent off`, one `<= Recv header`
  per head line (34, 20, 21, 25, 2 bytes), the two `Received 101` lines, `<= Recv data, 11 bytes (0xb)`
  `0000: ..hello....`, then the payload `hello` `03 e8` itself, `<= Recv data, 0 bytes (0x0)` (the
  read that saw the close), `* shutting down connection #0`. `--trace -` is the same with hex.
- Failure endings, same server: a `404` head gets `* Refused WebSocket upgrade: 404` between the
  last header and the blank `< ` line, then `* closing connection #0` (exit 22); a `101` with no
  frame gets `{ [0 bytes data]`, `* Empty reply from server`, `* shutting down connection #0` (exit 52).
- Built: `WsProtocolHandler` reports `using HTTP/1.x`, the request as one header event,
  `Request completely sent off`, each head line, the two `Received 101` lines and the ending;
  `WsFrameReceiver` reports each read as data received (the bytes that came with the head when
  there are any, and the empty read at the close). `Curl.Output.UnitLibrary` needed no change: its
  writers already fold consecutive data lines for `-v` and dump a 0-byte event as a title line.
- Decided (rule 1): a failure other than 22 and 52 writes its message then `closing connection #N`,
  as measured for `Recv failure: Connection was reset`; the upload frame is reported as data sent
  plus `upload completely sent off: N bytes`. curl's `[WS]` frame-error lines and its read-first
  order around `-T` were measured but differ from this; filed as BL-813.
- Tests: `WsProtocolHandlerEventTests` (7), `CurlCommandRunnerWsTransferEventTests` (5; the random
  key is checked as base64 of 16 bytes then replaced by the measured one). Ws 171 passed.
  `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary`: 100% line, 100% branch,
  0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -v, -i and --trace/--trace-ascii on ws:// write curl 8.21.0's upgrade, head, 101, data and closing lines
