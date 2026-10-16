---
id: BL-2471
title: Ws: public client frame codec with per-frame metadata and an upgrade-only connect for CONNECT_ONLY 2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2471 — Ws: public client frame codec with per-frame metadata and an upgrade-only connect for CONNECT_ONLY 2

## Goal

`Curl.Protocol.Ws` gives the managed engine what libcurl's WebSocket API (`curl_ws_recv`, `curl_ws_send`, `curl_ws_meta`) needs: a public way to run the upgrade and stop after the `101`, handing back the upgraded `IConnection` and the bytes that came with the reply head, and a public client frame codec that encodes a frame with any opcode and FIN bit (fragments via `CURLWS_CONT`) and decodes incrementally with per-frame metadata (opcode, FIN, offset, bytes left, ping completed for auto-pong).

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Found by BL-2443 (Engine: IEasyWebSocket), which depends on this. Today `WsFrameEncoder` only writes FIN frames, and `WsFrameDecoder`/`WsFrameReceiver` are `internal`, merge every payload into one run and give no per-frame metadata; `WsProtocolHandler.ExecuteAsync` always reads frames until the server closes. Keep the existing tool behaviour byte-identical (curl 8.21.0 measured, ADR-0128, ADR-0131): add the new public surface beside it and reuse the same decoding checks and violation messages (exit 56). The Ws library must not reference `Curl.Api` (protocol libraries reference only `Curl.Protocol.Abstractions`), so the metadata types are its own; the engine maps them to `CurlWebSocketFrame`/`CurlWebSocketFlags`.

## Acceptance criteria

- [ ] A public upgrade-only entry point performs the connect and upgrade exactly as today (same request bytes, `-v` lines, refusal exits) and on a `101` returns the open connection and the already-received bytes instead of reading frames; tests use a scripted fake connection.
- [ ] A public encoder writes masked frames for text, binary, continuation (FIN clear), close, ping and pong; tests pin the bytes for each, including a 126- and a 65536-byte payload.
- [ ] A public incremental decoder reports, for each run of payload, the frame's opcode, FIN, offset within the frame and bytes left, across a frame split over reads and several frames in one read, and reports a completed ping; violations fail with the same messages as `WsFrameDecoder`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened; `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` unchanged or grown and passing.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 0 failing members; every new public member has an XML doc comment and `Curl.Protocol.Ws.UnitLibrary/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
