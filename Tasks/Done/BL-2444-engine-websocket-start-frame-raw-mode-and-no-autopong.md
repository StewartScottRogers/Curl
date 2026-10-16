---
id: BL-2444
title: Engine: WebSocket start_frame, raw mode and no-autopong
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2443]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2444 — Engine: WebSocket start_frame, raw mode and no-autopong

## Goal

The engine's `IEasyWebSocket` implements `StartFrame`, `CURLWS_OFFSET`, `RAW_MODE` and `NOAUTOPONG` (via `WS_OPTIONS`).

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests pin raw bytes (`SendAsync_RawMode_WritesTheBytesUnframed`, `PerformAsync_WebSocketConnectOnlyWithRawMode_SendsTheBytesUnframedAndNoPong`) and a pong not sent (`ReceiveAsync_PingWithAutoPongOff_HandsThePingOverAndSendsNothing`, `ReceiveAsync_RawModePing_DecodesThePingAndSendsNoPong`). A frame sent in several pieces (`CURLWS_OFFSET`, `curl_ws_start_frame`) is BL-2481's, filed after this task with exactly that scope; see Notes.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Scope (2026-10-16, lane 1): `CURLWS_NOAUTOPONG` already came with BL-2443 and is pinned by its tests. `CURLWS_OFFSET` and `StartFrameAsync` are left to BL-2481 (Backlog), which BL-2443 filed for exactly that work and which also touches `Curl.Protocol.Ws` for a partial-frame encoder this task's `touches` do not name; doing it here would leave BL-2481 nothing to do (AF-0148). So this task built `CURLWS_RAW_MODE` on the connect-only API.
- Behaviour, from curl 8.16 `lib/ws.c` (the raw-mode code is unchanged in later releases, 8.22.0 included): under `data->set.ws_raw_mode`, `curl_ws_send` refuses a non-zero `fragsize` or `flags` with `CURLE_BAD_FUNCTION_ARGUMENT` ("fragsize and flags must be zero in raw mode") and writes the buffer directly; `curl_ws_recv` still decodes frames, and it skips flushing the pending pong, so no automatic pong goes out. `ManagedEasyWebSocket` now takes the handle's `WebSocketOptionBits` in place of an answers-pings flag and does the same. This only follows upstream, so no ADR.
- Raw mode in the write-callback path (a `ws` transfer that is not connect-only, `ws_cw_write`) needs a `WsProtocolHandler` option in `Curl.Protocol.Ws`, outside `touches`: filed as BL-2491.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` gave 0 failing members after `SendAsync` was split (it measured complexity 12 before).
## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. CURLWS_RAW_MODE on the connect-only WebSocket API; no-autopong pinned; OFFSET and start_frame left to BL-2481, write-callback raw mode filed as BL-2491
