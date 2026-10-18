---
id: BL-2672
title: Keep one easy handle's HSTS cache out of another handle's CURLOPT_HSTSWRITEFUNCTION so upstream case 1915 passes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2672 — Keep one easy handle's HSTS cache out of another handle's CURLOPT_HSTSWRITEFUNCTION so upstream case 1915 passes

## Goal

An easy handle's `CURLOPT_HSTSWRITEFUNCTION` is handed only that handle's HSTS entries at `curl_easy_cleanup`, and upstream case 1915 passes.

## Context

Found by BL-2545 (lib1915 port). lib1915's first handle loads four entries through its read callback and writes them at cleanup; its second, new handle's read callback fails (`CURLE_ABORTED_BY_CALLBACK`) and its write callback must be handed nothing. Case 1915's `<verify><stdout>` differs at byte 171 (line 6): expected `Second request returned 42`, got `[0/4] 1.example.com 25250320 01:02:03` - the second handle's cleanup writes the first handle's entries again, so the HSTS cache outlives its handle (look at `Curl.Engine.UnitLibrary/CallbackHstsCache.cs` and where the engine keeps the cache).

## Acceptance criteria

- [ ] A new easy handle whose HSTS read callback fails hands its write callback no entries at cleanup, with a focused unit test in `Curl.Engine.UnitTests`.
- [ ] Case 1915 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
