---
id: BL-2615
title: Run upstream case 558, which the harness skips as lacking the TrackMemory feature
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2615 — Run upstream case 558, which the harness skips as lacking the TrackMemory feature

## Goal

Upstream case 558 runs through the libtest runner instead of being skipped, and is on the ratchet when it passes.

## Context

Filed by BL-2531 (lib558 ported, `Lib558.cs`). The harness skips case 558: `Curl lacks the feature TrackMemory`. The case checks the memory-debug log of `curlx_malloc(558)` and `curl_easy_escape`; decide (ADR) whether the managed runner reports TrackMemory and how it answers the case's `<file>` memdump verify, or record why it stays excluded.

## Acceptance criteria

- [ ] Case 558 is either on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` passing, or excluded with an ADR giving the reason.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 62c257d5 4272a6b9.
