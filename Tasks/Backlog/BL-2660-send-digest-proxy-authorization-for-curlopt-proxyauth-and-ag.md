---
id: BL-2660
title: Send Digest Proxy-Authorization for CURLOPT_PROXYAUTH and again after a proxy change (case 1588)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2660 — Send Digest Proxy-Authorization for CURLOPT_PROXYAUTH and again after a proxy change (case 1588)

## Goal

`CURLOPT_PROXYAUTH` `CURLAUTH_DIGEST` with `CURLOPT_PROXYUSERPWD` answers a proxy's 407 with a Digest `Proxy-Authorization`, and does so again on the same handle after `CURLOPT_PROXY` changes, so upstream case 1588 (lib1588) passes.

## Context

Found by BL-2542, which ported lib1588. The runner reports `differs at byte 217 (line 8): expected "Proxy-Authorization: Digest username=\"silly\", realm=\"weirdorealm\", nonce=\"12345\", uri=\"/path/1588\", response=\"d0b2f000c7e3fca24452b5810713404a\"\r\n", got "Accept: */*\r\n"`: no Digest answer to the 407 is sent through Libcurl.

## Acceptance criteria

- [ ] Upstream case 1588 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project covers the proxy Digest answer (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
