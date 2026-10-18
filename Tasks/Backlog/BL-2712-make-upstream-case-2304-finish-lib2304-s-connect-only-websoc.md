---
id: BL-2712
title: Make upstream case 2304 finish lib2304's connect-only WebSocket rounds with exit 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2688]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2712 — Make upstream case 2304 finish lib2304's connect-only WebSocket rounds with exit 0

## Goal

Upstream case 2304 passes through the case runner and holds the ratchet.

## Context

Found by BL-2688, which made the WebSocket libtest cases run (ADR-0678: the harness stands in for `CURL_ENTROPY`, so `Sec-WebSocket-Key` now matches). Measured on Windows with `CURL_UPSTREAM_CASE=2304` and `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`: `test2304 fails: <verify><errorcode>: expected exit code 0, got 52`. `lib2304` (`Curl.Conformance.LibTest.UnitLibrary/Lib2304.cs`) upgrades with `CURLOPT_CONNECT_ONLY` 2 and runs `curl_ws_send` / `curl_ws_recv` ping rounds against a server that closes; upstream ends 0 (the case notes the outgoing PING may already fail), Curl answers `CURLE_GOT_NOTHING`. Find whether the 52 comes from the upgrade perform (`WebSocketUpgradeOnlyHandler`, the 101 reply handling in `WsProtocolHandler.ConnectAndUpgradeOnlyAsync`) or the port's return code, and narrow `touches` before editing.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=2304` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 2304 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
