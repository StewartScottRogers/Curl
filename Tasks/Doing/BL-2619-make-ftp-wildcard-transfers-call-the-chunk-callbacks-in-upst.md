---
id: BL-2619
title: Make FTP wildcard transfers call the chunk callbacks in upstream cases 576 and 1114
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2619 — Make FTP wildcard transfers call the chunk callbacks in upstream cases 576 and 1114

## Goal

Upstream cases 576 and 1114 (lib576, an FTP wildcard download with CURLOPT_CHUNK_BGN_FUNCTION and CURLOPT_CHUNK_END_FUNCTION) print the chunk callbacks' file details and pass through the runner.

## Context

Found by BL-2532: both cases run (no skip) but lib576 writes nothing to stdout, `<verify><stdout> differs at byte 0: expected "=====...\n", got the end` - the chunk begin callback is never called. `Lib576.ChunkPrinter` is unit tested and matches lib576.c. In the first pass, find whether the engine's wildcard transfer (BL-2478) or the runner's FTP stand-in drops the listing, and add that project to `touches` before any edit.

## Acceptance criteria

- [ ] Each case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] No added case's output depends on the platform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 57abe33b 21e5ddeb 4272a6b9.
