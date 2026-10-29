---
id: BL-4253
title: WebSocket: let an upload stream send whole text or binary messages, one frame each
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.WebSocket.UnitLibrary/IWsUploadMessageSource.cs, Curl.WebSocket.UnitLibrary/WsUploadMessage.cs, Curl.WebSocket.UnitTests/WsUploadMessageTests.cs, Curl.Protocol.Ws.UnitLibrary/WsProtocolHandler.cs, Curl.Protocol.Ws.UnitTests/WsProtocolHandlerMessageUploadTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4253 — WebSocket: let an upload stream send whole text or binary messages, one frame each

## Goal

An extension can hand curl's ws/wss handler an upload stream that sends a sequence of whole messages, each as one masked frame with its own opcode (text or binary), through a seam in the shared layer `Curl.WebSocket.UnitLibrary`.

## Context

Found by BL-4114 (the graphql-transport-ws, WAMP and STOMP subprotocols extension). Today `WsProtocolHandler.SendUploadAsync` (`Curl.Protocol.Ws.UnitLibrary/WsProtocolHandler.cs`) frames any `ITransferContext.Upload` that is not an `IWsUploadFrameSource` into binary frames of up to 64 KiB, so several messages written to the upload become one binary frame. graphql-transport-ws, WAMP's `wamp.2.json` and STOMP over WebSocket each need one text frame per message. The only per-frame seam, `IWsUploadFrameSource`/`WsUploadFrames`, lives in `Curl.Protocol.Ws`, which an extension may not reference (ADR-0999, `VerifyExtensionIsolation`), and the Engine uses it for `curl_ws_start_frame`, so leave it where it is. Add a sibling in the shared layer instead: `IWsUploadMessageSource` (an upload stream that also gives its messages: `IReadOnlyList<WsUploadMessage>`, or an async sequence) and `WsUploadMessage(WsOpcode Opcode, ReadOnlyMemory<byte> Payload)`. When the upload is an `IWsUploadMessageSource`, the handler sends each message as one final masked frame of its opcode (with `WsFrameEncoder` and the handler's random source), in order, then receives as it does now. Any other upload is unchanged, so no curl behaviour or upstream case changes.

## Acceptance criteria

- [x] `IWsUploadMessageSource` and `WsUploadMessage` exist in `Curl.WebSocket.UnitLibrary`, with XML doc comments.
- [x] A ws transfer whose upload is an `IWsUploadMessageSource` giving a text message "a" and a text message "b" sends exactly two masked final text frames, "a" then "b", before receiving; a unit test in `Curl.Protocol.Ws.UnitTests` with a fake connection checks the bytes.
- [x] Upload size and progress report the sum of the message payloads; a unit test.
- [x] Every existing `Curl.Protocol.Ws.UnitTests` test passes unchanged (any other upload is framed as before).
- [x] `Curl.WebSocket.UnitLibrary` and `Curl.Protocol.Ws.UnitLibrary` meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: `IWsUploadMessageSource` (a list of `WsUploadMessage`, not an async sequence: the extensions know their messages up front, and a list keeps the loop simple) and `WsUploadMessage` in `Curl.WebSocket.UnitLibrary`; `WsProtocolHandler.SendMessagesAsync` sends each as one final masked frame of its opcode, traced and logged like the read-by-read frames. Recorded in ADR-1041.
- Under `RawMode` a message source sends its stream bytes unframed, as any upload does (raw means no framing); pinned by a test.
- Upload size, request size and progress count the sum of the payloads as the criterion asks, unlike the read-by-read path, which counts frame bytes.
- Measure-CodeQuality.ps1 -Library Curl.WebSocket.UnitLibrary,Curl.Protocol.Ws.UnitLibrary: 0 failing members. Full build clean; fast tests green (Curl.Protocol.Ws.UnitTests 398 passed, Curl.WebSocket.UnitTests 79 passed).
- The Linux and macOS criterion is met by keeping the change platform-neutral (no paths, OS errors or certificates in the tests).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 4fe5696f aa5cf62a 5b6103f7 42825a71 679cdc1e 3d836d5e.
- 2026-10-29: Doing -> Done. An IWsUploadMessageSource upload sends each WsUploadMessage as one final masked frame of its own opcode
