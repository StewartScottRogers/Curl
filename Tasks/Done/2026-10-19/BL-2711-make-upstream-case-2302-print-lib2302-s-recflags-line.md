---
id: BL-2711
title: Make upstream case 2302 print lib2302's RECFLAGS line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2688]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-19
---
# BL-2711 — Make upstream case 2302 print lib2302's RECFLAGS line

## Goal

Upstream case 2302 passes through the case runner and holds the ratchet.

## Context

Found by BL-2688, which made the WebSocket libtest cases run (ADR-0678: the harness stands in for `CURL_ENTROPY`, so `Sec-WebSocket-Key` and the masks now match). Measured on Windows with `CURL_UPSTREAM_CASE=2302` and `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`: `test2302 fails: <verify><stdout> differs at byte 17 (line 2): expected "RECFLAGS: 1\r\n", got the end`. The first line (`68 65 6c 6c 6f `) matches, so `lib2302` (`Curl.Conformance.LibTest.UnitLibrary/Lib2302.cs`, `Lib2302Frames.cs`) gathers the frame but never prints the flags line `curl_ws_meta` should give at the frame's end. Check whether the fault is the engine's `curl_ws_meta` in a write callback (`ManagedEasyWebSocket.CurrentFrame` on an ordinary perform) or the port's output, and narrow `touches` before editing (add `Curl.Conformance.LibTest.UnitLibrary` if it is the port, checking other Doing tasks first). Note the expected stdout has `mode="text"`.

## Acceptance criteria

- [x] `CURL_UPSTREAM_CASE=2302` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 2302 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's `.UnitTests` project pins the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fault: the engine, not the port. `ManagedEasyWebSocket.CurrentFrame` was set only by `ReceiveAsync` (connect-only), so on an ordinary perform `curl_ws_meta` gave NULL and lib2302 wrote `RECFLAGS: NULL` to stderr instead of `RECFLAGS: 1` to stdout.
- Fix: `WsPerformConnection` (Ws library) now also carries `Frame` (the `WsFrameRun` of the payload being written) and `MessageOpcode`. With it set and raw mode off, `WsFrameReceiver` decodes each read with `WsFrameMetadataDecoder` beside the tool decoder and writes the payload a run at a time (ping runs and empty runs left out, so the same bytes are written), showing each run first. `CurrentFrame` answers that frame while options are not connect-only. The curl tool passes no `PerformConnection`, so its writes are unchanged.
- Decision (default taken): a write is one frame's run within one read, as libcurl's `ws_cw_write` hands each decoded frame chunk to the client writer with `recvframe` set; raw mode shows no frame (`curl_ws_meta` NULL), as `ws->recvframe` is untouched there.
- Tests: `WsFrameReceiverTests.ReceiveAsync_WithPerformConnection_WritesEachFramesRunAloneShowingItsFrame`, `ReceiveAsync_WithPerformConnectionInRawMode_ShowsNoFrame`, `ManagedEasyWebSocketTests.CurrentFrame_FromTheWriteCallbackOfAPerformThatIsNotConnectOnly_IsTheFrameOfThePayloadWritten`. Case 2302 is in-memory WebSocket bytes only, platform-neutral. Measure-CodeQuality for Engine and Ws: 100/100, 0 failing members.
- Touches not narrowed: the narrowing commit and push were refused by this run's permissions, so the whole-project entries stayed (too broad is safe).

## Log

- 2026-10-17: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b1eff55a f0471b8d 3d836d5e.
- 2026-10-19: Doing -> Done. Upstream case 2302 passes: curl_ws_meta from an ordinary perform's write callback answers the frame of the payload written
