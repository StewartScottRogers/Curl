---
id: BL-2712
title: Make upstream case 2304 finish lib2304's connect-only WebSocket rounds with exit 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2688]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-19
---
# BL-2712 — Make upstream case 2304 finish lib2304's connect-only WebSocket rounds with exit 0

## Goal

Upstream case 2304 passes through the case runner and holds the ratchet.

## Context

Found by BL-2688, which made the WebSocket libtest cases run (ADR-0678: the harness stands in for `CURL_ENTROPY`, so `Sec-WebSocket-Key` now matches). Measured on Windows with `CURL_UPSTREAM_CASE=2304` and `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`: `test2304 fails: <verify><errorcode>: expected exit code 0, got 52`. `lib2304` (`Curl.Conformance.LibTest.UnitLibrary/Lib2304.cs`) upgrades with `CURLOPT_CONNECT_ONLY` 2 and runs `curl_ws_send` / `curl_ws_recv` ping rounds against a server that closes; upstream ends 0 (the case notes the outgoing PING may already fail), Curl answers `CURLE_GOT_NOTHING`. Find whether the 52 comes from the upgrade perform (`WebSocketUpgradeOnlyHandler`, the 101 reply handling in `WsProtocolHandler.ConnectAndUpgradeOnlyAsync`) or the port's return code, and narrow `touches` before editing.

## Acceptance criteria

- [x] `CURL_UPSTREAM_CASE=2304` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 2304 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's `.UnitTests` project pins the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The 52 came from the upgrade perform, not the port: test2304's `101` head is LF-only with
  `nonewline`, so it has no blank line, and the server closes; `WsUpgradeResponseReader` fails that
  with `GotNothing`, which `WebSocketUpgradeOnlyHandler` passed on. curl 8.22.0's `Curl_http_done`
  checks for an empty reply only when `!data->set.connect_only`, so a connect-only perform answers 0
  with no upgrade, and lib2304's PING send fails and ends its rounds.
- Fixed in the engine (`WebSocketUpgradeOnlyHandler` maps `GotNothing` to success with the report),
  not the WebSocket library, whose outcome stays the tool's exit 52 (ADR-0705). `touches` narrowed to
  drop `Curl.Protocol.Ws.*`, which needed no change.
- Unit test: `ManagedEasyTransferTests.PerformAsync_WebSocketServerClosesBeforeTheHeadEnds_AnswersOkAndLeavesNoWebSocket`.
  `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members. The case is
  in-memory and platform-neutral (no paths, OS errors or certificates).

## Log

- 2026-10-17: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: a434ce3d f0471b8d 3d836d5e.
- 2026-10-19: Doing -> Done. Connect-only WebSocket perform answers Ok when the server closes before the head ends; upstream case 2304 on the ratchet
