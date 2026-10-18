---
id: BL-2688
title: Run upstream WebSocket libtest cases 2301 to 2304 with a Debug stand-in for CURL_ENTROPY
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2688 — Run upstream WebSocket libtest cases 2301 to 2304 with a Debug stand-in for CURL_ENTROPY

## Goal

Upstream cases 2301, 2302, 2303 and 2304 run (not skipped) through the case runner, and those that pass on Windows hold the ratchet.

## Context

Found by BL-2549, which ported lib2301 (raw-mode WebSocket write callback answering a PING), lib2302 (frames gathered with `curl_ws_meta`, serving cases 2302 and 2303) and lib2304 (connect-only WebSocket ping/pong rounds) in `Curl.Conformance.LibTest.UnitLibrary`. Measured on Windows: each is `skipped: Curl lacks the feature Debug`. The cases say "require Debug for the forced CURL_ENTROPY" and set `CURL_ENTROPY=12345678`, which upstream's debug build uses to fix the WebSocket key and frame masks. `UpstreamConformanceTests.cs` already stands in for Debug for other cases; extend the stand-in to a case naming the `ws` feature and make the engine the libtest runner hands the program honour `CURL_ENTROPY` for the `Sec-WebSocket-Key` and masks, finding the project at fault and adding it to `touches` before any edit. Record the decision in an ADR if it is a behaviour choice.

## Acceptance criteria

- [x] Cases 2301 to 2304 through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` are not skipped.
- [x] Each that passes is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that fails is filed as a task with its differing line.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Touches: added `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests`. An ordinary WebSocket perform built its handler with a hard-wired `SystemWebSocketRandomSource` in `EngineComposition.CreateProtocolHandlers`, so the key could not be fixed from the harness; no other Doing task on `origin/work/dark-factory` named either project.
- Decision (ADR-0678): `ManagedCurlEngine`'s internal constructor takes an optional `IWebSocketRandomSource`, one per engine as `CURL_ENTROPY`'s sequence is one per process; the harness passes `ForcedEntropyWebSocketRandomSource` (upstream `randit`) when a case sets `CURL_ENTROPY`, and reports `Debug` to a case naming `ws` and setting it.
- Measured on Windows: 2303 passes (on the ratchet). 2301 `<protocol> differs at byte 196 (line 10): expected "\x8A\x00", got the end` -> BL-2710. 2302 `<stdout> differs at byte 17 (line 2): expected "RECFLAGS: 1\r\n", got the end` -> BL-2711. 2304 `<errorcode>: expected exit code 0, got 52` -> BL-2712.
- 2303 compares only request headers and exit 22: nothing platform dependent.
- Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: a5ed17df 4f56d8e5 3d836d5e.
- 2026-10-17: Doing -> Done. Cases 2301-2304 run with the CURL_ENTROPY stand-in (ADR-0678); 2303 holds the ratchet; 2301, 2302, 2304 filed as BL-2710, BL-2711, BL-2712.
