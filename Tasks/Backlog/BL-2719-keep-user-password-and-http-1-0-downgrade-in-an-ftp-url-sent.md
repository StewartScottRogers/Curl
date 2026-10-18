---
id: BL-2719
title: Keep user:password and HTTP/1.0 downgrade in an FTP URL sent through an HTTP proxy (upstream cases 208, 299 and 1077)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
---
# BL-2719 — Keep user:password and HTTP/1.0 downgrade in an FTP URL sent through an HTTP proxy (upstream cases 208, 299 and 1077)

## Goal

Upstream cases 208, 299 and 1077 pass and are on the ratchet.

## Context

Filed by BL-2610. 208 and 299 expect `ftp://user:pass@host/...` kept in the proxied request line (Curl sends `ftp://host/...`); 1077 expects `HTTP/1.0` after a proxy downgrade (Curl sends HTTP/1.1). Run with `CURL_UPSTREAM_CASE=208` etc.

## Acceptance criteria

- [ ] The cases pass through the runner and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, each fix with a focused unit test in the fixed library's `.UnitTests` (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-18: Created.
