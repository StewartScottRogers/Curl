---
id: BL-2608
title: Send the proxy authentication retry for upstream cases 540 (Digest over multi) and 548 (NTLM POST)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2608 — Send the proxy authentication retry for upstream cases 540 (Digest over multi) and 548 (NTLM POST)

## Goal

Upstream cases 540 and 548 pass through the runner and are on the ratchet.

## Context

Found by BL-2530 (lib540 and lib547 ports). Case 540 (two transfers through one multi handle via an HTTP proxy, `CURLAUTH_ANY`, Digest from the proxy): `<verify><protocol> differs at byte 209 (line 8): expected "Proxy-Authorization: Digest username=\"silly\", realm=\"weirdorealm\", ... uri=\"/path/540\" ...", got "Accept: */*"` - the second request carries no Digest answer. Case 548 (POST with `CURLOPT_POSTFIELDS` via an HTTP proxy, Basic|Digest|NTLM): `differs at byte 236 (line 9): expected "POST http://test.remote.example.com/path/548 HTTP/1.1", got the end` - the follow-up NTLM request is never sent. Find where in the engine (HTTP proxy authentication, possibly the multi path) the retry is lost; the `touches` are a first guess - correct them before the first edit.

## Acceptance criteria

- [ ] Cases 540 and 548 pass through the runner and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482), and the cases' output does not depend on the platform.

## Notes

## Log

- 2026-10-17: Created.
