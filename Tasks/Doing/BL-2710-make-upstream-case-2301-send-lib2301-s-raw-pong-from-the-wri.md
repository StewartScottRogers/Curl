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
completed:
---
# BL-2710 — Make upstream case 2301 send lib2301's raw PONG from the write callback

## Goal

Upstream case 2301 passes through the case runner and holds the ratchet.

## Context

Found by BL-2688, which made the WebSocket libtest cases run (ADR-0678: the harness stands in for `CURL_ENTROPY`, so `Sec-WebSocket-Key` now matches). Measured on Windows with `CURL_UPSTREAM_CASE=2301` and `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`: `test2301 fails: <verify><protocol> differs at byte 196 (line 10): expected "\x8A\x00", got the end`. The server sends a PING (`89 00`) after the 101; `lib2301` (`Curl.Conformance.LibTest.UnitLibrary/Lib2301.cs`) runs in `CURLWS_RAW_MODE` and its write callback answers with `curl_ws_send` of the raw PONG bytes `8a 00`, which never reaches the server. Start at `ManagedEasyWebSocket.SendAsync` called during an ordinary (not connect-only) perform and `WsProtocolHandler`'s raw mode; find the project at fault before editing and narrow `touches` to it.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=2301` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 2301 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: a5362456 f0471b8d 3d836d5e.
