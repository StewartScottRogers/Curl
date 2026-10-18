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
completed:
---
# BL-2698 — Run upstream WebSocket libtest cases 2700 to 2723 with the Debug stand-in and hold those that pass

## Goal

Upstream cases 2700 to 2723 (all run `lib2700`) run, not skipped, through the case runner, and those that pass on Windows hold the ratchet.

## Context

Found by BL-2551, which ported lib2700 (`Curl.Conformance.LibTest.UnitLibrary/Lib2700.cs` with `Lib2700Frames`, the connect-only WebSocket echo loop). Measured on Windows: every case is `skipped: Curl lacks the feature Debug`. They need `<features>Debug ws</features>` and set `CURL_WS_FORCE_ZERO_MASK=1`, a debug-build switch making libcurl mask frames with zero, so `<protocol>` holds the exact echo bytes. BL-2688 extends the runner's Debug stand-in to `ws` cases for `CURL_ENTROPY`; once it is Done, extend the same stand-in to honour `CURL_WS_FORCE_ZERO_MASK` (find the project at fault and add it to `touches` before any edit; ADR if it is a behaviour choice). Run each with `CURL_UPSTREAM_CASE=<n>` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test.

## Acceptance criteria

- [ ] Each of cases 2700 to 2723 runs through the runner on Windows without being skipped for the Debug feature.
- [ ] Every one of them that passes is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that fails is filed as a task with its differing lines.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: cdfffd96 d6080d4c 3d836d5e.
