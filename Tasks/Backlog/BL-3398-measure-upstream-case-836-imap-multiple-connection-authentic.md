---
id: BL-3398
title: Measure upstream case 836 (IMAP multiple connection authentication) against curl 8.22.0 and make Curl match it
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-23
completed:
---
# BL-3398 — Measure upstream case 836 (IMAP multiple connection authentication) against curl 8.22.0 and make Curl match it

## Goal

Upstream case 836 either passes through Curl and is on the ratchet, or a measurement against the reference curl 8.22.0 shows Curl already matches it and the case's expected bytes are what is stale, and that is recorded.

## Context

Stewart (2026-10-23) wants every test passing. Case 836 is disabled upstream (`tests/data/DISABLED`: the test server does not behave as the case needs, ADR-0737). Command: `imap://HOST:PORT/836/;MAILINDEX=1 -u user.one:secret -: imap://HOST:PORT/836/;UID=2 -u user.two:secret`. On Windows Curl sends `B004 UID FETCH 2 BODY[]` where the case expects `B004 FETCH 2 BODY[]`. curl's documentation for the `UID` URL part makes `;UID=2` a UID fetch, so the case may be stale rather than Curl wrong. Measure first (standing rule): run the same command against the reference (`/ucrt64/bin/curl` 8.22.0, Schannel build, ADR-0018) with `Record-CurlExchange.ps1`, extending it for an IMAP server only if it cannot already record this exchange, and compare Curl's bytes to curl's. Library: `Curl.Protocol.Imap.UnitLibrary`.

## Acceptance criteria

- [ ] A Notes entry gives the recorded request bytes of the reference curl 8.22.0 and of Curl for the second connection of case 836 (the FETCH or UID FETCH command, and the AUTH exchange).
- [ ] If they differ, Curl is fixed to send curl's bytes, with a focused unit test in `Curl.Protocol.Imap.UnitTests` that fails without the fix (ADR-0482).
- [ ] If curl and Curl send the same bytes, a new ADR (decided by Claude under Stewart's delegation, listed in the Decisions README.md) amending ADR-0737 states that 836 stays off the ratchet because the case's expected output does not match curl 8.22.0 itself, with the measured bytes; otherwise case 836 ends `Passed` with `$env:CURL_UPSTREAM_CASE='836'` on Windows and is on `PassingUpstreamCases.txt` after ten passing runs.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-23: Created.
