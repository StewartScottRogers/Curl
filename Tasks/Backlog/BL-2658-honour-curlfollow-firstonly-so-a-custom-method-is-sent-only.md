---
id: BL-2658
title: Honour CURLFOLLOW_FIRSTONLY so a custom method is sent only on the first request (cases 1574, 1575, 1578, 1580)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2658 — Honour CURLFOLLOW_FIRSTONLY so a custom method is sent only on the first request (cases 1574, 1575, 1578, 1580)

## Goal

`CURLOPT_FOLLOWLOCATION` set to `CURLFOLLOW_FIRSTONLY` (3) sends a `CURLOPT_CUSTOMREQUEST` method on the first request only, so upstream cases 1574 and 1575 (lib1571) pass, and 1578 and 1580 (lib1576) once BL-2659 is done.

## Context

Found by BL-2542. Case 1574: `differs at byte 59 (line 5): expected "GET /15740001 HTTP/1.1\r\n", got "IGLOO /15740001 HTTP/1.1\r\n"`; case 1575: `differs at byte 130 (line 7): expected "mooPOST /15750001 HTTP/1.1\r\n", got "mooIGLOO /15750001 HTTP/1.1\r\n"`. `CURLFOLLOW_OBEYCODE` (cases 1571 to 1573 and 1581) already passes. libcurl 8.22.0 `lib/transfer.c` and `lib/http.c` (`data->set.http_follow_mode`).

## Acceptance criteria

- [ ] Upstream cases 1574 and 1575 pass through the runner and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in `Curl.Engine.UnitTests` covers the first-only method (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
