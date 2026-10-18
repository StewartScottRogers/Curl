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
completed:
---
# BL-2656 — Run upstream case 1567: a CURLOPT_CURLU handle followed twice sends no request

## Goal

Upstream case 1567 (lib1567: `CURLOPT_CURLU` with `CURLOPT_FOLLOWLOCATION`, performed twice) passes through the runner and holds the ratchet.

## Context

Found by BL-2542, which ported lib1567. The runner reports `<verify><protocol> differs at byte 0 (line 1): expected "GET /1567 HTTP/1.1\r\n", got the end`: no request is sent when the URL comes from a `CURLU` handle. BL-2643 is the same cause for case 1543; finish it first, then check 1567 still differs only in what it adds (a second perform of the same handle).

## Acceptance criteria

- [ ] Upstream case 1567 passes through the runner (`CURL_UPSTREAM_CASE=1567`, `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`) and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
