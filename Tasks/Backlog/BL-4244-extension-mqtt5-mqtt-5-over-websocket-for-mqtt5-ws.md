---
id: BL-4244
title: Extension Mqtt5: MQTT 5 over WebSocket for mqtt5+ws
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4242, BL-4243]
touches: [Curl.Extension.Mqtt5.UnitLibrary/WebSocket, Curl.Extension.Mqtt5.UnitTests/WebSocket, Curl.Extension.Mqtt5.UnitLibrary/Curl.Extension.Mqtt5.UnitLibrary.csproj]
requirement: none
created: 2026-10-29
completed:
---
# BL-4244 — Extension Mqtt5: MQTT 5 over WebSocket for mqtt5+ws

## Goal

`mqtt5+ws://host[:port]/topic` runs BL-4243's publish/subscribe flow over a WebSocket connection (subprotocol `mqtt`), as OASIS MQTT Version 5.0 section 6 describes.

## Context

Split from BL-4115 (extension 14 of 63) for size by its first pass. Specification: OASIS MQTT Version 5.0 section 6 and RFC 6455. Use the shared frame codec `Curl.WebSocket.UnitLibrary` (`WsFrameEncoder`, `WsFrameDecoder`, `WsOpcode`, `IWebSocketRandomSource`); never `Curl.Protocol.Ws.UnitLibrary` or another extension. Decide, and record in an ADR, the default port (80), the WebSocket request path (part of the URL path, or a fixed `/mqtt`) and how the topic is carried, since the WebSocket request and the MQTT topic both want the URL path. Do not register the scheme in any catalogue here: BL-4115 does that.

## Acceptance criteria

- [ ] The RFC 6455 opening handshake is sent with `Sec-WebSocket-Protocol: mqtt` and the response is validated (section 6: the server must select `mqtt`); unit tests with recorded bytes.
- [ ] MQTT packets travel in binary frames, a packet spanning frames or a frame holding several packets is handled (section 6), and a text frame closes the connection; unit tests over a fake `IConnection`.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the port, path and topic choices.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4115's lane 6 prerequisite pass.

## Log

- 2026-10-29: Created.
