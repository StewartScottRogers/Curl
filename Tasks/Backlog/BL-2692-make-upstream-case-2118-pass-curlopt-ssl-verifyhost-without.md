---
id: BL-2692
title: Make upstream case 2118 pass: CURLOPT_SSL_VERIFYHOST without VERIFYPEER rejects a wrong-name certificate with exit 60
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2692 — Make upstream case 2118 pass: CURLOPT_SSL_VERIFYHOST without VERIFYPEER rejects a wrong-name certificate with exit 60

## Goal

Upstream case 2118 passes through the case runner and holds the ratchet, or is pinned per platform.

## Context

Found by BL-2549, which ported lib2118 (`CURLOPT_SSL_VERIFYPEER` 0 and `CURLOPT_SSL_VERIFYHOST` 1 against a server whose certificate is for `localhost.nn`). Measured on Windows: `<verify><errorcode>: expected exit code 60, got 52` - Curl accepts the wrong-name certificate when the peer is not verified, then gets no reply; curl 8.22.0 checks the host name independently of the peer (`CURLE_PEER_FAILED_VERIFICATION`). In the first pass find the project at fault (the TLS host-name check in the engine) and add it to `touches` before any edit. The case involves a certificate, so check its output on Linux and macOS before holding it on the ratchet; pin each platform's answer if they differ.

## Acceptance criteria

- [ ] Case 2118 passes through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the difference left is filed as a task.
- [ ] The fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
