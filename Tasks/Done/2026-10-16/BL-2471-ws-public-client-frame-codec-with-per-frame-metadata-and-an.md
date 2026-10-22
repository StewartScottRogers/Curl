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
completed: 2026-10-16
---
# BL-2471 — Ws: public client frame codec with per-frame metadata and an upgrade-only connect for CONNECT_ONLY 2

## Goal

`Curl.Protocol.Ws` gives the managed engine what libcurl's WebSocket API (`curl_ws_recv`, `curl_ws_send`, `curl_ws_meta`) needs: a public way to run the upgrade and stop after the `101`, handing back the upgraded `IConnection` and the bytes that came with the reply head, and a public client frame codec that encodes a frame with any opcode and FIN bit (fragments via `CURLWS_CONT`) and decodes incrementally with per-frame metadata (opcode, FIN, offset, bytes left, ping completed for auto-pong).

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Found by BL-2443 (Engine: IEasyWebSocket), which depends on this. Today `WsFrameEncoder` only writes FIN frames, and `WsFrameDecoder`/`WsFrameReceiver` are `internal`, merge every payload into one run and give no per-frame metadata; `WsProtocolHandler.ExecuteAsync` always reads frames until the server closes. Keep the existing tool behaviour byte-identical (curl 8.21.0 measured, ADR-0128, ADR-0131): add the new public surface beside it and reuse the same decoding checks and violation messages (exit 56). The Ws library must not reference `Curl.Api` (protocol libraries reference only `Curl.Protocol.Abstractions`), so the metadata types are its own; the engine maps them to `CurlWebSocketFrame`/`CurlWebSocketFlags`.

## Acceptance criteria

- [x] A public upgrade-only entry point performs the connect and upgrade exactly as today (same request bytes, `-v` lines, refusal exits) and on a `101` returns the open connection and the already-received bytes instead of reading frames; tests use a scripted fake connection.
- [x] A public encoder writes masked frames for text, binary, continuation (FIN clear), close, ping and pong; tests pin the bytes for each, including a 126- and a 65536-byte payload.
- [x] A public incremental decoder reports, for each run of payload, the frame's opcode, FIN, offset within the frame and bytes left, across a frame split over reads and several frames in one read, and reports a completed ping; violations fail with the same messages as `WsFrameDecoder`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened; `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` unchanged or grown and passing.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 0 failing members; every new public member has an XML doc comment and `Curl.Protocol.Ws.UnitLibrary/CLAUDE.md` is kept true.

## Notes

- Plan (done by the lane itself, no subagents, to keep the cost down): reuse the tool's decoder with a `recordsRuns` flag so the public decoder has the same checks and messages; add a FIN parameter to the existing encoder; split `WsProtocolHandler`'s accept step so the same upgrade code either exchanges frames or hands the connection back. Decision recorded in ADR-0607.
- New public surface: `WsProtocolHandler.ConnectAndUpgradeOnlyAsync` returning `WsUpgradeOutcome`; `WsFrameEncoder.Encode(opcode, isFinal, payload, randomSource)`; `WsFrameMetadataDecoder` returning `WsDecodedFrames` of `WsFrameRun`; `WsOpcode` made public. `WsConnectionLease` (internal) closes the connection unless it is handed back.
- Decision: after the `101` the upgrade-only path writes the `Received 101` lines and the chunk-size trace line but not `websocket established, callback mode`, and on success does not log the transfer's end (the engine carries on). Not measured against a libcurl CONNECT_ONLY build; BL-2443 pins it if curl differs.
- Gotcha: `cond ? ping : null` typed as `ReadOnlyMemory<byte>` turns null into an empty memory through the array conversion, so `CompletedPing` uses `default(ReadOnlyMemory<byte>?)`.
- Measured: the first `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` run had 2 failing members (`UpgradeAsync` complexity 12; `ConnectAndUpgradeAsync` branch 83% from an await in `finally`), fixed by extracting `AcceptAsync` and the lease; the second run had 0 failing members, 100% line and branch. Ws tests: 428 passed, 5 skipped (already skipped before); the full fast run is green. `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Ws has an upgrade-only connect for CONNECT_ONLY 2, a public FIN-aware frame encoder and a metadata frame decoder for curl_ws_recv/send/meta
