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
completed:
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

- [ ] `WsProtocolHandlerEventTests` pins the three `[WS]` lines and `closing connection #0` for an invalid opcode, and the measured lines for each other violation `WsFrameDecoder` detects (measured first).
- [ ] `WsProtocolHandlerEventTests` pins the `-T` event order as curl 8.21.0 writes it against a server that holds the connection open.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
