---
id: BL-2710
title: Make upstream case 2301 send lib2301's raw PONG from the write callback
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2688]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-19
---
# BL-2710 — Make upstream case 2301 send lib2301's raw PONG from the write callback

## Goal

Upstream case 2301 passes through the case runner and holds the ratchet.

## Context

Found by BL-2688, which made the WebSocket libtest cases run (ADR-0678: the harness stands in for `CURL_ENTROPY`, so `Sec-WebSocket-Key` now matches). Measured on Windows with `CURL_UPSTREAM_CASE=2301` and `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`: `test2301 fails: <verify><protocol> differs at byte 196 (line 10): expected "\x8A\x00", got the end`. The server sends a PING (`89 00`) after the 101; `lib2301` (`Curl.Conformance.LibTest.UnitLibrary/Lib2301.cs`) runs in `CURLWS_RAW_MODE` and its write callback answers with `curl_ws_send` of the raw PONG bytes `8a 00`, which never reaches the server. Start at `ManagedEasyWebSocket.SendAsync` called during an ordinary (not connect-only) perform and `WsProtocolHandler`'s raw mode; find the project at fault before editing and narrow `touches` to it.

## Acceptance criteria

- [x] `CURL_UPSTREAM_CASE=2301` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 2301 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's `.UnitTests` project pins the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fault: `ManagedEasyWebSocket.SendAsync` answered `UnsupportedProtocol` on any handle not connect-only, so lib2301's `curl_ws_send` from its write callback never reached the server. Fix: `Curl.Protocol.Ws.UnitLibrary`'s new `WsPerformConnection`, which `WsProtocolHandler.PerformConnection` fills from the `101` until the transfer ends; the handle's `ManagedEasyWebSocket.PerformConnection` is passed through the dispatcher factory (sixth argument), `ManagedCurlEngine.CreateDispatcher` and `EngineComposition.CreateProtocolHandlers`, and `SendAsync` writes to it during a perform that is not connect-only. Receives are unchanged.
- Decision (default taken): the send writes straight to the connection, as `ws_send_raw` does under raw mode; in framed mode it writes the masked frame directly too rather than queueing it as `lib/ws.c` does; only raw mode is measured (case 2301). Outside a perform it still answers `UnsupportedProtocol`, as before.
- Tests: `WsProtocolHandlerTests.ExecuteAsync_WithPerformConnection_ShowsTheConnectionWhileFramesFlowSoTheOutputCanSendOnIt` and `ManagedEasyWebSocketTests.SendAsync_FromTheWriteCallbackOfAPerformThatIsNotConnectOnly_WritesToTheConnectionItsFramesFlowOn`. Case 2301 is in-memory WebSocket bytes only, no path or OS text, so it is platform-neutral. Measure-CodeQuality for Engine and Ws: 100/100, 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: a5362456 f0471b8d 3d836d5e.
- 2026-10-19: Doing -> Done. Upstream case 2301 passes: curl_ws_send from the write callback of an ordinary WebSocket perform reaches the server
