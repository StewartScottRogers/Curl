---
id: BL-2782
title: Make upstream case 2708 pass: keep an automatically answered PING from curl_ws_recv
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
---
# BL-2782 — Make upstream case 2708 pass: keep an automatically answered PING from curl_ws_recv

## Goal

Upstream case 2708 (`ws: Automatic PONG`, `lib2700`) passes on Windows and is on the ratchet.

## Context

Found by BL-2698, which ran cases 2700 to 2723 under the harness's `CURL_WS_FORCE_ZERO_MASK` stand-in (ADR-0696): all
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

- [ ] `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes with `CURL_UPSTREAM_CASE=2708` on Windows.
- [ ] 2708 is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused test in `Curl.Engine.UnitTests` pins that, with automatic PONG on, a received PING is answered and not returned by the receive call (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
