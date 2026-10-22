---
id: BL-3002
title: Escape a double quote in DIGEST-MD5's user name and realm in curl's own form so upstream case 907 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/SaslDigestMd5.cs, Curl.Authentication.UnitTests/SaslDigestMd5Tests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-3002 — Escape a double quote in DIGEST-MD5's user name and realm in curl's own form so upstream case 907 passes

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

Found by BL-2834 (ADR-0751): test907 (user us"er, realm cu"rl) differs at line 3: curl's own DIGEST-MD5 answer sends username="us\"er",realm="cu\"rl"; Curl sends the quote unescaped. Add a focused unit test in Curl.Authentication.UnitTests (ADR-0482).

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-21: Created.
