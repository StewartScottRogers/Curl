---
id: BL-2657
title: Run upstream case 1568: CURLOPT_PORT with Digest auth leaves the port out of the Host header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2606]
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2657 — Run upstream case 1568: CURLOPT_PORT with Digest auth leaves the port out of the Host header

## Goal

Upstream case 1568 (lib1568: Digest auth with `CURLOPT_PORT` set apart from the URL) passes through the runner and holds the ratchet.

## Context

Found by BL-2542, which ported lib1568. The runner reports `<verify><protocol> differs at byte 35 (line 2): expected "Host: 127.0.0.1:8990\r\n", got "Host: 127.0.0.1\r\n"`. BL-2606 honours `CURLOPT_PORT` in the request line, Host header and connect; finish it first, then check the Digest retry carries the port too.

## Acceptance criteria

- [ ] Upstream case 1568 passes through the runner (`CURL_UPSTREAM_CASE=1568`) and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: ed23954b 48be2d2b 3d836d5e.
