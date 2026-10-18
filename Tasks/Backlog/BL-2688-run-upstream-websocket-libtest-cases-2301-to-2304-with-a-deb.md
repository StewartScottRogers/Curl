---
id: BL-2688
title: Run upstream WebSocket libtest cases 2301 to 2304 with a Debug stand-in for CURL_ENTROPY
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2688 — Run upstream WebSocket libtest cases 2301 to 2304 with a Debug stand-in for CURL_ENTROPY

## Goal

Upstream cases 2301, 2302, 2303 and 2304 run (not skipped) through the case runner, and those that pass on Windows hold the ratchet.

## Context

Found by BL-2549, which ported lib2301 (raw-mode WebSocket write callback answering a PING), lib2302 (frames gathered with `curl_ws_meta`, serving cases 2302 and 2303) and lib2304 (connect-only WebSocket ping/pong rounds) in `Curl.Conformance.LibTest.UnitLibrary`. Measured on Windows: each is `skipped: Curl lacks the feature Debug`. The cases say "require Debug for the forced CURL_ENTROPY" and set `CURL_ENTROPY=12345678`, which upstream's debug build uses to fix the WebSocket key and frame masks. `UpstreamConformanceTests.cs` already stands in for Debug for other cases; extend the stand-in to a case naming the `ws` feature and make the engine the libtest runner hands the program honour `CURL_ENTROPY` for the `Sec-WebSocket-Key` and masks, finding the project at fault and adding it to `touches` before any edit. Record the decision in an ADR if it is a behaviour choice.

## Acceptance criteria

- [ ] Cases 2301 to 2304 through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` are not skipped.
- [ ] Each that passes is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that fails is filed as a task with its differing line.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
