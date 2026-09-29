---
id: BL-813
title: Write curl's [WS] frame-error lines and -T upload order for -v on a WebSocket transfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-584]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-813 — Write curl's [WS] frame-error lines and -T upload order for -v on a WebSocket transfer

## Goal

`-v` on a `ws://` transfer that hits a frame violation, or that sends a `-T` upload, writes the lines curl 8.21.0 writes, in curl's order.

## Context

- Measured in BL-584 (curl 8.21.0 Schannel, `Record-CurlExchange.ps1`, 2026-09-28), reply the 101 head used there:
  - `-sv` with frames `81 02 "ok"` `0f 00` (opcode 0xf): after `{ [6 bytes data]` curl writes `* [WS] invalid opcode: 0f`, `* [WS] decode frame error 56`, `* [WS] decode payload error 56`, `* closing connection #0`, exit 56. `WsProtocolHandler` now writes the `WsTransferException` message instead of the three `[WS]` lines.
  - `-sv -T up.txt` (4 bytes) with `81 02 "ok"`: curl writes `{ [4 bytes data]` (the `} [10 bytes data]` it sent is folded into that line), then `* upload completely sent off: 10 bytes`, then `* Recv failure: Connection was reset`, `* closing connection #0`, exit 56 (the recorder closed while curl still sent). Curl reads before it sends the frame; `WsProtocolHandler` sends first and reports `} [10 bytes data]`, `* upload completely sent off: 10 bytes`, then `{ [4 bytes data]`. Re-measure with a server that holds the connection open (`-HoldOpenMilliseconds`) before pinning the order.
- Events: `ITransferEvents` (ADR-0046); `WsFrameDecoder`, `WsFrameReceiver`, `WsProtocolHandler.SendUploadAsync`. ADR-0131.

## Acceptance criteria

- [x] `WsProtocolHandlerEventTests` pins the three `[WS]` lines and `closing connection #0` for an invalid opcode, and the measured lines for each other violation `WsFrameDecoder` detects (measured first).
- [x] `WsProtocolHandlerEventTests` pins the `-T` event order as curl 8.21.0 writes it against a server that holds the connection open.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-09-29, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -HoldOpenMilliseconds`, reply `81 02 "ok"` then the violation. Every violation `WsFrameDecoder` detects gives the same shape: `{ [N bytes data]`, `* [WS] <message>`, `* [WS] decode frame error 56`, `* [WS] decode payload error 56`, `* closing connection #0`, exit 56; `-S` prints only `curl: (56) [WS] <message>`. Messages measured: `invalid opcode: 0f`, `invalid reserved bits: c1`, `no ongoing fragmented message to resume`, `fragmented message interrupted by new TEXT msg`, `invalid fragmented PING frame`, `masked input frame`, `received PING frame is too big`, `frame length longer than 63 bits not supported` - all already the decoder's text.
- `-T` against a held-open server, `--trace-ascii`: `Recv data, 4 bytes` (the frame that came with the 101 head), `Send data, 10 bytes`, `upload completely sent off: 10 bytes`, `Recv data, 0 bytes`, `shutting down connection #0`, exit 0. With nothing after the head curl sends first, then `Recv data, 0 bytes`, `Empty reply from server`, exit 52. `-v` shows no `} [10 bytes data]` line because curl writes one data line per run of data events; `VerboseTransferEventWriter` already folds the same way (`dataLineWritten`).
- Change: `WsFrameReceiver.ReceiveAsync` split into `DeliverAlreadyReceivedAsync` and `ReceiveUntilClosedAsync`, so the handler delivers the head's leftover bytes, sends the upload, then reads. `WsTransferException.IsFrameViolation` marks decoder violations; the handler writes the two decode-error lines after the message for those only. The progress order of an upload with a frame in the head is now `down` then `up`, following the same measurement. No ADR: this matches measured curl rather than choosing between behaviours.


## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. -v on ws:// writes curl's [WS] decode-error lines for every frame violation, and -T sends after the frame that came with the 101 head, as curl 8.21.0 does
