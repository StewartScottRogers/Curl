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
completed:
---
# BL-2711 — Make upstream case 2302 print lib2302's RECFLAGS line

## Goal

Upstream case 2302 passes through the case runner and holds the ratchet.

## Context

Found by BL-2688, which made the WebSocket libtest cases run (ADR-0678: the harness stands in for `CURL_ENTROPY`, so `Sec-WebSocket-Key` and the masks now match). Measured on Windows with `CURL_UPSTREAM_CASE=2302` and `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`: `test2302 fails: <verify><stdout> differs at byte 17 (line 2): expected "RECFLAGS: 1\r\n", got the end`. The first line (`68 65 6c 6c 6f `) matches, so `lib2302` (`Curl.Conformance.LibTest.UnitLibrary/Lib2302.cs`, `Lib2302Frames.cs`) gathers the frame but never prints the flags line `curl_ws_meta` should give at the frame's end. Check whether the fault is the engine's `curl_ws_meta` in a write callback (`ManagedEasyWebSocket.CurrentFrame` on an ordinary perform) or the port's output, and narrow `touches` before editing (add `Curl.Conformance.LibTest.UnitLibrary` if it is the port, checking other Doing tasks first). Note the expected stdout has `mode="text"`.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=2302` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 2302 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
