---
id: BL-4053
title: Fix SslStreamTlsProviderTests SelfSignedServerUnderInsecure NegotiatedGroupName null under the coverage run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests/SslStreamTlsProviderTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4053 — Fix SslStreamTlsProviderTests SelfSignedServerUnderInsecure NegotiatedGroupName null under the coverage run

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

The test shared host name with earlier tests, so a resumed session reported no key share and NegotiatedGroupName was null; it now uses its own host like the TLS 1.2 test. Fast tests in Curl.Networking.UnitTests pass.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-29: Doing -> Done. Own target host so no resumed session hides the key share
