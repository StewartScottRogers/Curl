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
completed:
---
# BL-2611 — Engine: WebSocket upload through the read callback, with curl_ws_start_frame inside it

## Goal

A `ws://` or `wss://` perform with `CURLOPT_UPLOAD` (not connect-only) sends what the read callback gives as WebSocket frames, as curl 8.22.0's `lib/ws.c` reader does, and `curl_ws_start_frame` called inside that read callback starts the frame the callback's bytes fill.

## Context

ADR-0665 (BL-2481) made `ManagedEasyWebSocket` send frames in parts under `CURLWS_OFFSET` and `StartFrameAsync`, but the engine runs WebSocket transfers only connect-only, where no read callback is called, so a frame started from a WebSocket upload's read callback has nowhere to go. Read `lib/ws.c` (the `ws_cw_*`/`cr_ws_*` reader, `curl_ws_start_frame`) for how the upload reader frames callback bytes (binary frames of the bytes read unless a frame was started) and how `WsProtocolHandler` sends uploads today. `WsFrameEncoder.EncodeHead` and `MaskPart` already write a frame in parts. Lane 5 first filed this on 2026-10-16 as BL-2521, on a branch that never reached the board.

## Acceptance criteria

- [ ] `ManagedEasyTransferTests.WebSocket.cs` uploads read-callback bytes over a `ws://` perform and the bytes written after the `101` are the frames `lib/ws.c` sends for them.
- [ ] A read callback that calls `StartFrameAsync(Text, n)` and then returns n bytes sends one text frame announcing n bytes, with a test.
- [ ] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Protocol.Ws.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-17: Created.
