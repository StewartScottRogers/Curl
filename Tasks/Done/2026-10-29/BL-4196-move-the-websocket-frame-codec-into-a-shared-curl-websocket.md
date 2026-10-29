---
id: BL-4196
title: Move the WebSocket frame codec into a shared Curl.WebSocket.UnitLibrary layer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.WebSocket.UnitLibrary, Curl.WebSocket.UnitTests, Curl.Protocol.Ws.UnitLibrary/WsFrameEncoder.cs, Curl.Protocol.Ws.UnitLibrary/WsFrameDecoder.cs, Curl.Protocol.Ws.UnitLibrary/WsFrameMetadataDecoder.cs, Curl.Protocol.Ws.UnitLibrary/WsOpcode.cs, Curl.Protocol.Ws.UnitLibrary/Curl.Protocol.Ws.UnitLibrary.csproj, Curl.Protocol.Ws.UnitTests/WsFrameEncoderTests.cs, Curl.Protocol.Ws.UnitTests/WsFrameDecoderTests.cs, Curl.Protocol.Ws.UnitTests/WsFrameMetadataDecoderTests.cs, Curl.slnx, Curl.Protocol.Ws.UnitLibrary/IWebSocketRandomSource.cs, Curl.Protocol.Ws.UnitLibrary/SystemWebSocketRandomSource.cs, Curl.Protocol.Ws.UnitLibrary/WsFrameRun.cs, Curl.Protocol.Ws.UnitLibrary/WsDecodedFrames.cs, Curl.Protocol.Ws.UnitLibrary/WsDecodedBytes.cs, Curl.Protocol.Ws.UnitLibrary/WsFrameReceiver.cs, Curl.Protocol.Ws.UnitLibrary/WsFrameDecodeReporter.cs, Curl.Protocol.Ws.UnitLibrary/CLAUDE.md, Curl.Protocol.Ws.UnitTests/Curl.Protocol.Ws.UnitTests.csproj, Curl.Protocol.Ws.UnitTests/SystemWebSocketRandomSourceTests.cs, Curl.Protocol.Ws.UnitTests/Fakes/FixedRandomSource.cs, Curl.Protocol.Ws.UnitTests/Fakes/ScriptedRandomSource.cs, Curl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs, Curl.Engine.UnitLibrary/Curl.Engine.UnitLibrary.csproj, Curl.Engine.UnitTests/Curl.Engine.UnitTests.csproj, Curl.Console/Curl.Console.csproj, Curl.Conformance.UnitTests/Curl.Conformance.UnitTests.csproj, Documentation/Planning/Decisions/ADR-0120-protocol-libraries-may-reference-the-hand-built-libraries-and-nothing-else-horizontal.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4196 — Move the WebSocket frame codec into a shared Curl.WebSocket.UnitLibrary layer

## Goal

The WebSocket frame encoder, decoder, metadata decoder and opcode (RFC 6455 section 5) live in a new shared layer `Curl.WebSocket.UnitLibrary` that `Curl.Protocol.Ws.UnitLibrary` and extensions both reference, with no change to what curl does.

## Context

Found by BL-4112's first pass: extensions may not reference `Curl.Protocol.*` (ADR-0999, `VerifyExtensionIsolation`), and BL-4112 (WebSocket over HTTP/2 and HTTP/3), BL-4111 (`--ws-repl`) and BL-4114 (WebSocket subprotocols) all need the frame codec now in `Curl.Protocol.Ws.UnitLibrary` (`WsFrameEncoder.cs`, `WsFrameDecoder.cs`, `WsFrameMetadataDecoder.cs`, `WsOpcode.cs`). Use the `new-project` skill. Move the matching unit tests with the code.

## Acceptance criteria

- [x] `Curl.WebSocket.UnitLibrary` and `Curl.WebSocket.UnitTests` exist in `Curl.slnx`, hold the frame codec and its tests, and reference no `Curl.Protocol.*` library.
- [x] `Curl.Protocol.Ws.UnitLibrary` uses the shared codec and no longer has its own copy; every `Curl.Protocol.Ws.UnitTests` test passes unchanged in behaviour.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decided (ADR-1017): the decoder depended on WsTransferLog, WsFrameTrace and WsTransferException (all on Curl.Protocol.Abstractions), so it now tells an IWsFrameDecodeObserver what it decodes and returns a ViolationMessage; Curl.Protocol.Ws's WsFrameDecodeReporter writes the same log and trace lines, and WsFrameReceiver turns the message into the same exit-56 WsTransferException. IWebSocketRandomSource, SystemWebSocketRandomSource, WsFrameRun, WsDecodedFrames and WsDecodedBytes moved too, since the codec's public signatures use them.
- Touches widened (no task in Doing on origin/work/dark-factory held any of them): the extra moved files, WsFrameReceiver.cs, the new WsFrameDecodeReporter.cs, the Ws CLAUDE.md, the isolation test and ADR-0120 (the new layer joins ADR-0120's table), and the csproj files of Engine, Engine.UnitTests, Console and Conformance.UnitTests, which import Curl.WebSocket with one <Using> rather than a using line in each file (EngineComposition.cs and CurlComposition.cs were held by other lanes).
- Fakes FixedRandomSource and ScriptedRandomSource moved to Curl.WebSocket.UnitTests/Fakes and are linked into Curl.Protocol.Ws.UnitTests. The moved decoder tests now check the violation message; exit 56 stays pinned by WsFrameReceiver's tests. Added WsFrameDecodeObserverTests.
- Measured: Measure-CodeQuality.ps1 -Library Curl.WebSocket.UnitLibrary,Curl.Protocol.Ws.UnitLibrary reports 0 failing members. Fast tests green on Windows (Ws 392, WebSocket 78, Engine, Console, Conformance all pass); the change adds nothing platform-specific, so CI on Linux and macOS is covered by keeping it platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: e418ffa8 fc07ca31 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. The WebSocket frame codec lives in the shared Curl.WebSocket.UnitLibrary, used by Curl.Protocol.Ws with unchanged behaviour
