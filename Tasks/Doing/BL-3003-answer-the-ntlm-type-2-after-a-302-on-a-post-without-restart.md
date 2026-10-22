---
id: BL-3003
title: Answer the NTLM type 2 after a 302 on a POST without restarting at type 1 so upstream case 1100 passes, and stop after the type 3 over HTTP/1.0 for case 159
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs, Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-3003 — Answer the NTLM type 2 after a 302 on a POST without restarting at type 1 so upstream case 1100 passes, and stop after the type 3 over HTTP/1.0 for case 159

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

Found by BL-2834 (ADR-0751), Debug now reported to both: test1100 differs at line 19 of <verify><protocol>: after the 302 Curl sends a new NTLM type 1 where curl sends the request without Authorization (expected "User-Agent: curl/8.22.0"). test159 ("known to fail" upstream, HTTP/1.0) differs at line 7: Curl sends another "GET /159 HTTP/1.0" where curl stops. Split if the two causes differ. Add focused unit tests (ADR-0482).

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: fbecace0.
