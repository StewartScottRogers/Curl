---
id: BL-2632
title: Make upstream cases 674 and 695 pass: CURLOPT_CURLU with duphandle, and a reused MIME structure as subparts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2628, BL-2626]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2632 — Make upstream cases 674 and 695 pass: CURLOPT_CURLU with duphandle, and a reused MIME structure as subparts

## Goal

Upstream cases 674 and 695 pass through the runner.

## Context

Found by BL-2535, which ported the libtest program and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. In the first pass find the project at fault and add it to `touches` before any edit. test674 (lib674: `CURLOPT_CURLU` only, no `CURLOPT_URL`, then `curl_easy_duphandle`): `differs at byte 0: expected "GET /674 HTTP/1.1", got the end` - the same cause as BL-2628 (`CURLOPT_CURLU` ignored). test695 (lib695: `CURLOPT_MIMEPOST` set to a new structure, then the first structure made its part's subparts): `Content-Length: 423` expected, `138` got - the post was taken at setopt time, BL-2626's cause. Re-run both once those are Done.

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef c59fcab3 3d836d5e.
