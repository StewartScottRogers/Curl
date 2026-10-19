---
id: BL-2787
title: Make upstream cases 569 and 571 pass: CURLOPT_RTSP_SESSION_ID NULL forgets the session, and RTSP interleave finishes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Curl.Api.UnitLibrary, Curl.Engine.UnitLibrary/HandleRtspSession.cs, Curl.Engine.UnitTests/HandleRtspSessionTests.cs, Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
---

## Goal

Upstream cases 569 and 571 pass through the runner and join `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Left by BL-2704, which carries the `CURLOPT_RTSP_*` options into the RTSP handler (`Curl.Engine.UnitLibrary/HandleRtspSession.cs`). Measured on Windows, 2026-10-18:

- test569: `<verify><protocol>` line 11, expected `Transport: Fake/NotReal/JustATest;foo=baz`, got `Session: 00.+1-am-aSe55ion_id\$yes-i-am\$`. `lib569` sets `CURLOPT_RTSP_SESSION_ID` to NULL after each TEARDOWN; libcurl forgets the session then. `EasyRtspOptions` is a record, so a NULL set over a NULL option is invisible to the engine, which hands an ID on only when the option changes. Needs a per-setopt signal (for example a set count on `EasyRtspOptions` that `Libcurl`'s `SetRtspSessionId` bumps, read by `HandleRtspSession`, which calls `RtspSessionState.ForgetSessionId`).
- test571: curl did not finish within 20 seconds (SETUP / PLAY / RECEIVE with `CURLOPT_INTERLEAVEFUNCTION`); find where the RECEIVE or interleaved read waits.

Debug with `CURL_UPSTREAM_CASE=<n>` and `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"`.

## Acceptance criteria

- [ ] test569 and test571 pass through the runner on Windows and are on `PassingUpstreamCases.txt`, or one that still fails is filed with its differing lines.
- [ ] Each fix has a focused unit test in its library's `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 42d7ebda fb1216c9 4272a6b9.
