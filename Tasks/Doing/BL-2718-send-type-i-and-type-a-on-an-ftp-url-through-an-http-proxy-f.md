---
id: BL-2718
title: Send ;type=i and ;type=a on an FTP URL through an HTTP proxy for CURLOPT_PROXY_TRANSFER_MODE (upstream cases 549 and 550)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
---
# BL-2718 — Send ;type=i and ;type=a on an FTP URL through an HTTP proxy for CURLOPT_PROXY_TRANSFER_MODE (upstream cases 549 and 550)

## Goal

Upstream cases 549 and 550 pass and are on the ratchet.

## Context

Filed by BL-2610, which made the harness report the ftp feature. Case 549 expects the proxied request line `GET ftp://www.example.com/moo/549;type=i HTTP/1.1` and 550 `...550;type=a`, but Curl sends the URL with no `;type=` suffix when CURLOPT_PROXY_TRANSFER_MODE is set. Run with `CURL_UPSTREAM_CASE=549` / `550`. 561 already passes.

## Acceptance criteria

- [ ] The cases pass through the runner and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, each fix with a focused unit test in the fixed library's `.UnitTests` (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: e6f6390a 4272a6b9.
