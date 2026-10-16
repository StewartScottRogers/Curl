---
id: BL-2494
title: Engine: CURLWS_RAW_MODE on a write-callback WebSocket transfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2494 — Engine: CURLWS_RAW_MODE on a write-callback WebSocket transfer

## Goal

A `ws` or `wss` perform from the options that is not connect-only, with `EasyWebSocketOptions.Options` holding `RawMode`, hands the write callback the WebSocket stream's bytes as they came, unframed, as curl 8.22.0's `ws_cw_write` passes them to the next writer under `data->set.ws_raw_mode`.

## Context

BL-2444 gave `ManagedEasyWebSocket` (the `CURLOPT_CONNECT_ONLY` 2 API) `CURLWS_RAW_MODE`: `curl_ws_send` writes the bytes unframed and refuses any flag or fragment size, and no automatic pong is sent. The other place `lib/ws.c` reads raw mode is the client writer of a transfer that is not connect-only, where frames are decoded before the write callback today. That path runs through `Curl.Protocol.Ws`'s `WsProtocolHandler`, which needs an option for it, and the engine must set that option from `EasyWebSocketOptions`. Read `lib/ws.c` (`ws_cw_write`, `ws_cw_init`) at curl 8.22.0 for what else raw mode skips there (the auto-pong, the close handling).

## Acceptance criteria

- [ ] An engine test performs a `ws://` transfer under `RawMode` over a fake connection and the write callback receives the server's frame bytes unchanged.
- [ ] Without `RawMode` the same transfer still hands over decoded payload (existing tests unchanged).
- [ ] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Protocol.Ws.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: bee24eaa 4f85dbc5 0f90c956.
