---
id: BL-2611
title: Engine: WebSocket upload through the read callback, with curl_ws_start_frame inside it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2481]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2611 — Engine: WebSocket upload through the read callback, with curl_ws_start_frame inside it

## Goal

A `ws://` or `wss://` perform with `CURLOPT_UPLOAD` (not connect-only) sends what the read callback gives as WebSocket frames, as curl 8.22.0's `lib/ws.c` reader does, and `curl_ws_start_frame` called inside that read callback starts the frame the callback's bytes fill.

## Context

ADR-0665 (BL-2481) made `ManagedEasyWebSocket` send frames in parts under `CURLWS_OFFSET` and `StartFrameAsync`, but the engine runs WebSocket transfers only connect-only, where no read callback is called, so a frame started from a WebSocket upload's read callback has nowhere to go. Read `lib/ws.c` (the `ws_cw_*`/`cr_ws_*` reader, `curl_ws_start_frame`) for how the upload reader frames callback bytes (binary frames of the bytes read unless a frame was started) and how `WsProtocolHandler` sends uploads today. `WsFrameEncoder.EncodeHead` and `MaskPart` already write a frame in parts. Lane 5 first filed this on 2026-10-16 as BL-2521, on a branch that never reached the board.

## Acceptance criteria

- [x] `ManagedEasyTransferTests.WebSocket.cs` uploads read-callback bytes over a `ws://` perform and the bytes written after the `101` are the frames `lib/ws.c` sends for them.
- [x] A read callback that calls `StartFrameAsync(Text, n)` and then returns n bytes sends one text frame announcing n bytes, with a test.
- [x] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Protocol.Ws.UnitLibrary` reports 0 failing members.

## Notes

- Plan (ADR-0669): `Curl.Protocol.Ws` gains `WsUploadFrames` (the upload's `ws->enc` and `ws->sendbuf`) and
  `IWsUploadFrameSource`. `WsProtocolHandler.SendFramedUploadAsync` sends an upload stream that carries frames
  read by read as `cr_ws_read`: each read of up to 64 KiB is one binary frame, unless `StartFrame` started one,
  whose payload the next reads fill (each read asked for no more than the payload left; excess bytes dropped as
  `ws_enc_write_payload` drops them; a read of nothing still sends a started head). The engine's
  `CallbackReadStream` carries the handle's `ManagedEasyWebSocket.UploadFrames`, and `StartFrameAsync` on a handle
  that is not connect-only starts the frame there (`SendError` outside an upload or while a frame is unfinished,
  `BadFunctionArgument` for bad bits).
- Default taken: 64 KiB per read, libcurl's default `CURLOPT_UPLOAD_BUFFERSIZE`; not measured, because a libcurl
  read callback cannot be driven from the command line.
- Default taken: the tool's `-T` keeps ADR-0131's one frame, so no tool output changes; a `-T` over 64 KiB is
  filed as BL-2683.
- Tests: `ManagedEasyTransferTests.WebSocket.cs` (three new), `WsProtocolHandlerFramedUploadTests` (eight),
  `WsUploadFramesTests` (five). Fast tests all green; quality run: both libraries 100% line and branch, 0 failing
  members.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: dc315ed6 75eda62f 0f90c956.
- 2026-10-17: Doing -> Done. A ws:// read-callback upload sends each read as a binary frame as lib/ws.c does, and curl_ws_start_frame inside the callback starts the frame its bytes fill
