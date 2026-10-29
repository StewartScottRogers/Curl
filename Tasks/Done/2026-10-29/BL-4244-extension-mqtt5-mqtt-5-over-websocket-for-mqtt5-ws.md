---
id: BL-4244
title: Extension Mqtt5: MQTT 5 over WebSocket for mqtt5+ws
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4242, BL-4243]
touches: [Curl.Extension.Mqtt5.UnitLibrary/WebSocket, Curl.Extension.Mqtt5.UnitTests/WebSocket, Curl.Extension.Mqtt5.UnitLibrary/Curl.Extension.Mqtt5.UnitLibrary.csproj, Curl.Extension.Mqtt5.UnitLibrary/CLAUDE.md, Curl.Extension.Mqtt5.UnitLibrary/Client/Mqtt5ProtocolHandler.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4244 — Extension Mqtt5: MQTT 5 over WebSocket for mqtt5+ws

## Goal

`mqtt5+ws://host[:port]/topic` runs BL-4243's publish/subscribe flow over a WebSocket connection (subprotocol `mqtt`), as OASIS MQTT Version 5.0 section 6 describes.

## Context

Split from BL-4115 (extension 14 of 63) for size by its first pass. Specification: OASIS MQTT Version 5.0 section 6 and RFC 6455. Use the shared frame codec `Curl.WebSocket.UnitLibrary` (`WsFrameEncoder`, `WsFrameDecoder`, `WsOpcode`, `IWebSocketRandomSource`); never `Curl.Protocol.Ws.UnitLibrary` or another extension. Decide, and record in an ADR, the default port (80), the WebSocket request path (part of the URL path, or a fixed `/mqtt`) and how the topic is carried, since the WebSocket request and the MQTT topic both want the URL path. Do not register the scheme in any catalogue here: BL-4115 does that.

## Acceptance criteria

- [x] The RFC 6455 opening handshake is sent with `Sec-WebSocket-Protocol: mqtt` and the response is validated (section 6: the server must select `mqtt`); unit tests with recorded bytes.
- [x] MQTT packets travel in binary frames, a packet spanning frames or a frame holding several packets is handled (section 6), and a text frame closes the connection; unit tests over a fake `IConnection`.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the port, path and topic choices.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4115's lane 6 prerequisite pass.
- Added `Curl.Extension.Mqtt5.UnitLibrary/CLAUDE.md` (its layout and references must stay true) and `Client/Mqtt5ProtocolHandler.cs` (exposes `ClientIdentifierPrefix` and `NewClientIdentifierSuffix` so the WebSocket handler makes the same client identifier) to touches; no task in Doing on origin/work/dark-factory names either.
- Plan and decisions (ADR-1054): `WebSocket/Mqtt5WebSocketUpgrade` sends the RFC 6455 handshake to a fixed `/mqtt` with `Sec-WebSocket-Protocol: mqtt` and checks 101, accept and the selected `mqtt`; `Mqtt5WebSocketConnection` is an `IConnection` carrying packets in binary frames (stream of binary payload, pong to ping, close frame as end, text frame or RFC 6455 violation fails the read so the handler closes the connection); `Mqtt5WebSocketProtocolHandler` serves `mqtt5+ws` on port 80 and runs `Mqtt5ClientFlow` unchanged. The topic stays the URL path, as for `mqtt5://`. Not registered: BL-4115 does that.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.Mqtt5.UnitLibrary` gives 100% line, 100% branch, 0 failing members; Curl.Extension.Mqtt5.UnitTests 194 passed; `dotnet build` clean and the fast tests green. The tests use only fake connections and byte arrays, nothing platform dependent, which keeps CI on Linux and macOS green.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 1ad396c3 931c9c48 9f4106c9 14de81cc.
- 2026-10-29: Doing -> Done. mqtt5+ws:// runs the MQTT 5 publish/subscribe flow over a WebSocket (subprotocol mqtt) in binary frames
