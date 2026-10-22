---
id: BL-3004
title: Answer DIGEST-MD5's rspauth with an empty line in curl's own form so upstream cases 823 and 869 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/SaslAuthenticator.cs, Curl.Authentication.UnitLibrary/SaslDigestMd5.cs, Curl.Authentication.UnitTests/SaslAuthenticatorTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-3004 — Answer DIGEST-MD5's rspauth with an empty line in curl's own form so upstream cases 823 and 869 pass

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

Found by BL-2834 (ADR-0751): with Debug and the forced cnonce, test823 and test869 differ at line 4 of <verify><protocol>: curl answers the rspauth challenge with an empty line, Curl sends "=". Run with $env:CURL_UPSTREAM_CASE='823'. Add a focused unit test in Curl.Authentication.UnitTests (ADR-0482).

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-21: Created.
