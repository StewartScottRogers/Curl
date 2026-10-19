---
id: BL-2782
title: Make upstream case 2708 pass: keep an automatically answered PING from curl_ws_recv
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed: 2026-10-19
---
# BL-2782 — Make upstream case 2708 pass: keep an automatically answered PING from curl_ws_recv

## Goal

Upstream case 2708 (`ws: Automatic PONG`, `lib2700`) passes on Windows and is on the ratchet.

## Context

Found by BL-2698, which ran cases 2700 to 2723 under the harness's `CURL_WS_FORCE_ZERO_MASK` stand-in (ADR-0698): all
pass but 2708, the one case where `Lib2700` leaves libcurl's automatic PONG on. Measured on Windows:

```
<verify><protocol> differs at byte 10 (line 1)
- 1: "\x8A\x84\x00\x00\x00\x00test\x88\x80\x00\x00\x00\x00"
+ 1: "\x8A\x84\x00\x00\x00\x00test\x89\x84\x00\x00\x00\x00test\x88\x80\x00\x00\x00\x00"
```

Curl sends the automatic PONG, then also hands the PING to `curl_ws_recv`, so `Lib2700` echoes it as a PING. The case
says "PING is handled by lib and never given to application": with auto-PONG on, upstream's `ws_recv` answers the PING
and does not return it. Start at `Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs` (its `AutoPong` handling); if the
fault is elsewhere, add that project to `touches` first. Run with `CURL_UPSTREAM_CASE=2708` and
`NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`.

## Acceptance criteria

- [x] `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes with `CURL_UPSTREAM_CASE=2708` on Windows.
- [x] 2708 is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused test in `Curl.Engine.UnitTests` pins that, with automatic PONG on, a received PING is answered and not returned by the receive call (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fix: `ManagedEasyWebSocket.DecodeAsync` (now through `QueueRuns`) leaves a ping's runs out of the receive queue
  whenever it answers pings (no `CURLWS_NOAUTOPONG`, no `CURLWS_RAW_MODE`), as upstream's `ws_client_collect`
  keeps an auto-answered PING from `curl_ws_recv`. A receive that decoded only a ping answers `Again`. Every run of
  the ping is left out, also a ping split across reads: the simplest faithful choice, since upstream only answers
  once the ping's payload is whole.
- New test `ReceiveAsync_PingWithAutoPongOn_AnswersThePingAndDoesNotHandItOver`; three existing tests that expected
  the ping to be handed over were changed to the new behaviour.
- Added `Curl.Engine.UnitLibrary/CLAUDE.md` to `touches` to keep its WebSocket section true; no task in Doing names it.
- Measure-CodeQuality (Curl.Engine.UnitLibrary): first run put `DecodeAsync` at complexity 12, so the queueing moved
  into `QueueRuns`; second run 100% line, 100% branch, 0 failing members, worst CRAP 10.
- Fast tests: all green (Engine 2066 passed, Conformance 3551 passed).

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: be1ee021 6d4654cb 70e3c171 3d836d5e.
- 2026-10-19: Doing -> Done. curl_ws_recv no longer hands over an automatically answered PING; upstream case 2708 passes and is on the ratchet
