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
completed: 2026-10-18
---
# BL-2619 — Make FTP wildcard transfers call the chunk callbacks in upstream cases 576 and 1114

## Goal

Upstream cases 576 and 1114 (lib576, an FTP wildcard download with CURLOPT_CHUNK_BGN_FUNCTION and CURLOPT_CHUNK_END_FUNCTION) print the chunk callbacks' file details and pass through the runner.

## Context

Found by BL-2532: both cases run (no skip) but lib576 writes nothing to stdout, `<verify><stdout> differs at byte 0: expected "=====...\n", got the end` - the chunk begin callback is never called. `Lib576.ChunkPrinter` is unit tested and matches lib576.c. In the first pass, find whether the engine's wildcard transfer (BL-2478) or the runner's FTP stand-in drops the listing, and add that project to `touches` before any edit.

## Acceptance criteria

- [x] Each case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- 2026-10-18 (lane 1): both cases already pass through the runner on Windows (`NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` with `CURL_UPSTREAM_CASE=576` and `1114`: "passes; add to PassingUpstreamCases.txt"). Work landed since BL-2532 measured the failure (the engine's `CallbackWildcardHooks`, BL-2420, and the connect-phase work up to BL-2623) now reaches the chunk callbacks, so no product change was needed and `touches` stays as filed. Platform check: the output is lib576 printing the fields the FTP stand-in's fixed Unix-style LIST text gives, with no local paths, OS error text or file system semantics, so it does not depend on the platform. Build clean; fast tests green (Curl.Conformance.UnitTests 3403 passed, 0 failed, ratchet holding both new rows).

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 57abe33b 21e5ddeb 4272a6b9.
- 2026-10-18: Doing -> Done. Upstream cases 576 and 1114 (FTP wildcard with chunk callbacks) pass and are on the ratchet
