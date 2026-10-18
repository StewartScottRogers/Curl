---
id: BL-2698
title: Run upstream WebSocket libtest cases 2700 to 2723 with the Debug stand-in and hold those that pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2688]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2698 — Run upstream WebSocket libtest cases 2700 to 2723 with the Debug stand-in and hold those that pass

## Goal

Upstream cases 2700 to 2723 (all run `lib2700`) run, not skipped, through the case runner, and those that pass on Windows hold the ratchet.

## Context

Found by BL-2551, which ported lib2700 (`Curl.Conformance.LibTest.UnitLibrary/Lib2700.cs` with `Lib2700Frames`, the connect-only WebSocket echo loop). Measured on Windows: every case is `skipped: Curl lacks the feature Debug`. They need `<features>Debug ws</features>` and set `CURL_WS_FORCE_ZERO_MASK=1`, a debug-build switch making libcurl mask frames with zero, so `<protocol>` holds the exact echo bytes. BL-2688 extends the runner's Debug stand-in to `ws` cases for `CURL_ENTROPY`; once it is Done, extend the same stand-in to honour `CURL_WS_FORCE_ZERO_MASK` (find the project at fault and add it to `touches` before any edit; ADR if it is a behaviour choice). Run each with `CURL_UPSTREAM_CASE=<n>` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test.

## Acceptance criteria

- [x] Each of cases 2700 to 2723 runs through the runner on Windows without being skipped for the Debug feature.
- [x] Every one of them that passes is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that fails is filed as a task with its differing lines.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The fault was in the harness only, so `touches` stayed `Curl.Conformance.UnitTests`. `PlatformFor` now reports
  `Debug` to a `ws` case setting `CURL_WS_FORCE_ZERO_MASK`, and `CreateEngine` gives the libtest engine a new
  `ZeroMaskWebSocketRandomSource` (zero 4-byte masks, random key) through ADR-0678's `IWebSocketRandomSource` seam.
  Decision recorded in ADR-0694.
- Measured on Windows: 23 of 24 pass and are on the ratchet. 2708 (`ws: Automatic PONG`) fails: Curl sends the
  automatic PONG and then also returns the PING to `curl_ws_recv`, which `lib2700` echoes. Filed as BL-2782 with the
  differing lines.
- Platform check: these cases exchange bytes only with the in-memory `sws`. No paths, OS error text or certificates
  are involved, so they are safe on Linux and macOS.
- No production library changed, so no Measure-CodeQuality run was needed.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: cdfffd96 d6080d4c 3d836d5e.
- 2026-10-18: Doing -> Done. Upstream WebSocket cases 2700 to 2723 run under a CURL_WS_FORCE_ZERO_MASK stand-in; 23 hold the ratchet, 2708 filed as BL-2782
