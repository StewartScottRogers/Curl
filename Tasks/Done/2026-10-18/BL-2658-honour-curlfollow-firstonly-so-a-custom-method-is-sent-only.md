---
id: BL-2658
title: Honour CURLFOLLOW_FIRSTONLY so a custom method is sent only on the first request (cases 1574, 1575, 1578, 1580)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/RedirectPolicyMapping.cs, Curl.Engine.UnitTests/RedirectPolicyMappingTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Core.UnitLibrary/RedirectPolicy.cs, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/RedirectFollowerTests.cs, Curl.Core.UnitTests/RedirectFollowerMethodSwitchLineTests.cs]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2658 — Honour CURLFOLLOW_FIRSTONLY so a custom method is sent only on the first request (cases 1574, 1575, 1578, 1580)

## Goal

`CURLOPT_FOLLOWLOCATION` set to `CURLFOLLOW_FIRSTONLY` (3) sends a `CURLOPT_CUSTOMREQUEST` method on the first request only, so upstream cases 1574 and 1575 (lib1571) pass, and 1578 and 1580 (lib1576) once BL-2659 is done.

## Context

Found by BL-2542. Case 1574: `differs at byte 59 (line 5): expected "GET /15740001 HTTP/1.1\r\n", got "IGLOO /15740001 HTTP/1.1\r\n"`; case 1575: `differs at byte 130 (line 7): expected "mooPOST /15750001 HTTP/1.1\r\n", got "mooIGLOO /15750001 HTTP/1.1\r\n"`. `CURLFOLLOW_OBEYCODE` (cases 1571 to 1573 and 1581) already passes. libcurl 8.22.0 `lib/transfer.c` and `lib/http.c` (`data->set.http_follow_mode`).

## Acceptance criteria

- [x] Upstream cases 1574 and 1575 pass through the runner and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in `Curl.Engine.UnitTests` covers the first-only method (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests to files, by reading RedirectPolicyMapping.DropsCustomMethodOnSwitchToGet and the FollowLocation option, so it can run beside other Engine tasks.
- 2026-10-18: The follower lives in Curl.Core, so touches gained Curl.Core.UnitLibrary/RedirectPolicy.cs and RedirectFollower.cs and two Core test files; no task in Doing on origin/work/dark-factory named them (only BL-2657: Libcurl). Plan: new `RedirectPolicy.SendsCustomMethodFirstOnly`, mapped from `FollowLocation.FirstOnly`; every redirect hop drops the `-X` method (body kept, so 1575's second request is POST), and no `Stick to M instead of GET` line, as libcurl 8.22.0's `http_switch_to_get` skips it under CURLFOLLOW_FIRSTONLY. Cases 1574 and 1575 pass through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; both are plain HTTP exchanges with no path or OS text, so platform-neutral. 1578 and 1580 left to BL-2659. Measure-CodeQuality: Curl.Core.UnitLibrary and Curl.Engine.UnitLibrary 100% line and branch, 0 failing members (the first-only check sits in `HopMethod` to keep `FollowChainAsync` at the complexity gate).

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: db059030 a9bb9769 3d836d5e.
- 2026-10-18: Doing -> Done. CURLFOLLOW_FIRSTONLY sends a custom method on the first request only; upstream cases 1574 and 1575 pass and are on the ratchet
