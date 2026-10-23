---
id: BL-3399
title: Measure upstream case 938 (SMTP multiple connection authentication) against curl 8.22.0 and make Curl match it
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-23
completed:
---
# BL-3399 — Measure upstream case 938 (SMTP multiple connection authentication) against curl 8.22.0 and make Curl match it

## Goal

Upstream case 938 either passes through Curl and is on the ratchet, or a measurement against the reference curl 8.22.0 shows Curl already matches it and the case's expected bytes are what is stale, and that is recorded.

## Context

Stewart (2026-10-23) wants every test passing. Case 938 is disabled upstream (the test server does not behave as the case needs, ADR-0737). Command: two SMTP uploads on one `-:` command line, `-u user.one:secret` for the first URL and `-u user.two:secret` for the second. On Windows the `AUTH PLAIN` initial response is `AHVzZXIub25lAHNlY3JldA==` where the case expects `dXNlci5vbmUAdXNlci5vbmUAc2VjcmV0`; read the case's `<protocol>` part for the exact expected lines. Decide which side is right by measuring (standing rule): run the same command against the reference curl 8.22.0 (`/ucrt64/bin/curl`, ADR-0018) with `Record-CurlExchange.ps1`, extending it for an SMTP exchange only if it cannot already record this one, and compare the bytes with Curl's. Library: `Curl.Protocol.Smtp.UnitLibrary`.

## Acceptance criteria

- [ ] A Notes entry gives the recorded `AUTH` lines of the reference curl 8.22.0 and of Curl for both connections of case 938.
- [ ] If they differ, Curl is fixed to send curl's bytes, with a focused unit test in `Curl.Protocol.Smtp.UnitTests` that fails without the fix (ADR-0482).
- [ ] If curl and Curl send the same bytes, a new ADR (decided by Claude under Stewart's delegation, listed in the Decisions README.md) amending ADR-0737 states that 938 stays off the ratchet because the case's expected output does not match curl 8.22.0 itself, with the measured bytes; otherwise case 938 ends `Passed` with `$env:CURL_UPSTREAM_CASE='938'` on Windows and is on `PassingUpstreamCases.txt` after ten passing runs.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-23: Created.
