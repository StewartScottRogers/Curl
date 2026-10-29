---
id: BL-4193
title: Move the WebSocket frame codec into a shared Curl.WebSocket.UnitLibrary layer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.WebSocket.UnitLibrary, Curl.WebSocket.UnitTests, Curl.Protocol.Ws.UnitLibrary/WsFrameEncoder.cs, Curl.Protocol.Ws.UnitLibrary/WsFrameDecoder.cs, Curl.Protocol.Ws.UnitLibrary/WsFrameMetadataDecoder.cs, Curl.Protocol.Ws.UnitLibrary/WsOpcode.cs, Curl.Protocol.Ws.UnitLibrary/Curl.Protocol.Ws.UnitLibrary.csproj, Curl.Protocol.Ws.UnitTests/WsFrameEncoderTests.cs, Curl.Protocol.Ws.UnitTests/WsFrameDecoderTests.cs, Curl.Protocol.Ws.UnitTests/WsFrameMetadataDecoderTests.cs, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4193 — Move the WebSocket frame codec into a shared Curl.WebSocket.UnitLibrary layer

## Goal

The WebSocket frame encoder, decoder, metadata decoder and opcode (RFC 6455 section 5) live in a new shared layer `Curl.WebSocket.UnitLibrary` that `Curl.Protocol.Ws.UnitLibrary` and extensions both reference, with no change to what curl does.

## Context

Found by BL-4112's first pass: extensions may not reference `Curl.Protocol.*` (ADR-0999, `VerifyExtensionIsolation`), and BL-4112 (WebSocket over HTTP/2 and HTTP/3), BL-4111 (`--ws-repl`) and BL-4114 (WebSocket subprotocols) all need the frame codec now in `Curl.Protocol.Ws.UnitLibrary` (`WsFrameEncoder.cs`, `WsFrameDecoder.cs`, `WsFrameMetadataDecoder.cs`, `WsOpcode.cs`). Use the `new-project` skill. Move the matching unit tests with the code.

## Acceptance criteria

- [ ] `Curl.WebSocket.UnitLibrary` and `Curl.WebSocket.UnitTests` exist in `Curl.slnx`, hold the frame codec and its tests, and reference no `Curl.Protocol.*` library.
- [ ] `Curl.Protocol.Ws.UnitLibrary` uses the shared codec and no longer has its own copy; every `Curl.Protocol.Ws.UnitTests` test passes unchanged in behaviour.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
