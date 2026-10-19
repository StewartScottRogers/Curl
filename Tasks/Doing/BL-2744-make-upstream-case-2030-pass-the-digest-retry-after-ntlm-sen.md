---
id: BL-2744
title: Make upstream case 2030 pass: the Digest retry after NTLM sends its Authorization header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2747]
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

- 2026-10-18 (lane 5, first pass, no code edited): the fault is not in the lib2023 port. Request 100 (NTLM, wrong password) ends on a 401 that also offers `Digest realm="testrealm", nonce="2"`; curl stores that nonce in `data->state.digest` even while Digest is not allowed and answers it on request 200 under `CURLAUTH_DIGEST`. Curl's `DigestAuthenticator` is stateless and no hook carries the challenge into the handle's next transfer - exactly BL-2747 (the same cause as case 2024, BL-2691). Added BL-2747 to `depends-on` and requeued; once it is Done, rerun `CURL_UPSTREAM_CASE=2030` and hold 2030 on the ratchet. Note request 400 is expected twice with nonce 5 (curl treats the first Digest 401 as a fresh challenge, not a failure), which BL-2747's design must also produce.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b96ecbf8 fd6bd9d4 3d836d5e.
- 2026-10-18: Doing -> Backlog. Waits on BL-2747: the handle must keep the last 401's Digest challenge for its next transfer
- 2026-10-18: Backlog -> Doing. Criteria at claim: b96ecbf8 fd6bd9d4 3d836d5e.
