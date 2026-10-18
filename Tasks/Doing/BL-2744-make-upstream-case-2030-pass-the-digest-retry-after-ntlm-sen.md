---
id: BL-2744
title: Make upstream case 2030 pass: the Digest retry after NTLM sends its Authorization header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
---
# BL-2744 — Make upstream case 2030 pass: the Digest retry after NTLM sends its Authorization header

## Goal

Upstream case 2030 (lib2023, libauthretry) passes through the case runner and holds the ratchet.

## Context

Found by BL-2689, which put 2025, 2029, 2031 and 2032 on the ratchet once BL-2635 fixed the NTLM type-1 flags. Measured on Windows with `CURL_UPSTREAM_CASE=2030`: `<verify><protocol> differs at byte 433 (line 13): expected "Authorization: Digest username=\"testuser\", realm=\"testrealm\", nonce=\"2\", uri=\"/20300200\", response=\"2f2d784ba53a0a307758a90e98d25c27\"\r\n", got "Accept: */*\r\n"` - the Digest request that should follow the changed auth scheme goes out without its Authorization header. Start with `Curl.Conformance.LibTest.UnitLibrary/Lib2023.cs` (how it switches `CURLOPT_HTTPAUTH` between requests) and the easy handle's auth state it drives; if the cause is in the HTTP auth engine, add that project to `touches`.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=2030` with `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 2030 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] The fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b96ecbf8 fd6bd9d4 3d836d5e.
