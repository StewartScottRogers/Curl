---
id: BL-2656
title: Run upstream case 1567: a CURLOPT_CURLU handle followed twice sends no request
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2643]
touches: [Libcurl, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2656 — Run upstream case 1567: a CURLOPT_CURLU handle followed twice sends no request

## Goal

Upstream case 1567 (lib1567: `CURLOPT_CURLU` with `CURLOPT_FOLLOWLOCATION`, performed twice) passes through the runner and holds the ratchet.

## Context

Found by BL-2542, which ported lib1567. The runner reports `<verify><protocol> differs at byte 0 (line 1): expected "GET /1567 HTTP/1.1\r\n", got the end`: no request is sent when the URL comes from a `CURLU` handle. BL-2643 is the same cause for case 1543; finish it first, then check 1567 still differs only in what it adds (a second perform of the same handle).

## Acceptance criteria

- [x] Upstream case 1567 passes through the runner (`CURL_UPSTREAM_CASE=1567`, `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`) and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- No product change needed: BL-2643's `UrlHandleOverride` fix already makes test1567 pass (the handle is read afresh on every perform, so the second perform starts from `/1567` again, not the redirect target). Added it to the ratchet; HTTP-only output, no platform dependence.
- Unit test: `ManagedEasyTransferTests.PerformAsync_UrlHandleFollowedThenPerformedAgain_StartsAgainFromTheHandlesUrl` (Curl.Engine.UnitTests). Only test code changed, so no Measure-CodeQuality run.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: db445757 48be2d2b 3d836d5e.
- 2026-10-18: Doing -> Done. A CURLOPT_CURLU handle followed and performed twice sends both requests from the handle's URL; test1567 holds the ratchet
