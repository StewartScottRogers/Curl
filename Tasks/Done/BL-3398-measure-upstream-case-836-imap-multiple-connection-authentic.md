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
completed: 2026-10-23
---
# BL-3398 — Measure upstream case 836 (IMAP multiple connection authentication) against curl 8.22.0 and make Curl match it

## Goal

Upstream case 836 either passes through Curl and is on the ratchet, or a measurement against the reference curl 8.22.0 shows Curl already matches it and the case's expected bytes are what is stale, and that is recorded.

## Context

Stewart (2026-10-23) wants every test passing. Case 836 is disabled upstream (`tests/data/DISABLED`: the test server does not behave as the case needs, ADR-0737). Command: `imap://HOST:PORT/836/;MAILINDEX=1 -u user.one:secret -: imap://HOST:PORT/836/;UID=2 -u user.two:secret`. On Windows Curl sends `B004 UID FETCH 2 BODY[]` where the case expects `B004 FETCH 2 BODY[]`. curl's documentation for the `UID` URL part makes `;UID=2` a UID fetch, so the case may be stale rather than Curl wrong. Measure first (standing rule): run the same command against the reference (`/ucrt64/bin/curl` 8.22.0, Schannel build, ADR-0018) with `Record-CurlExchange.ps1`, extending it for an IMAP server only if it cannot already record this exchange, and compare Curl's bytes to curl's. Library: `Curl.Protocol.Imap.UnitLibrary`.

## Acceptance criteria

- [x] A Notes entry gives the recorded request bytes of the reference curl 8.22.0 and of Curl for the second connection of case 836 (the FETCH or UID FETCH command, and the AUTH exchange).
- [x] If they differ, Curl is fixed to send curl's bytes, with a focused unit test in `Curl.Protocol.Imap.UnitTests` that fails without the fix (ADR-0482).
- [x] If curl and Curl send the same bytes, a new ADR (decided by Claude under Stewart's delegation, listed in the Decisions README.md) amending ADR-0737 states that 836 stays off the ratchet because the case's expected output does not match curl 8.22.0 itself, with the measured bytes; otherwise case 836 ends `Passed` with `$env:CURL_UPSTREAM_CASE='836'` on Windows and is on `PassingUpstreamCases.txt` after ten passing runs.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured with `Record-CurlExchange.ps1 -Imap`, `-ImapReply` giving upstream's greeting and capability list (no `AUTH=`, so LOGIN), the second URL alone (`-s 'imap://127.0.0.1:PORT/836/;UID=2' -u user.two:secret`), because the recorder serves one session per run; the tag letter per connection (`B`) is BL-1987's and already matches. Reference curl 8.22.0 (ucrt64, Schannel) and Curl (Curl.Console Debug build) sent the same 100 request bytes (hashes equal), both exit 0: `A001 CAPABILITY` / `A002 LOGIN user.two secret` / `A003 SELECT 836` / `A004 UID FETCH 2 BODY[]` / `A005 LOGOUT`. The case expects `FETCH 2 BODY[]`, so its expected bytes are stale.
- No fix: the bytes match, so criterion 2 (if they differ) does not apply and nothing in Curl.Protocol.Imap.UnitLibrary changed, so no quality run was needed. ADR-0802 amends ADR-0737: 836 stays off the ratchet.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: a8b6cb20 fecb3cea 36ac2ccf c5f8cef6.
- 2026-10-23: Doing -> Done. Measured case 836: curl 8.22.0 and Curl both send UID FETCH 2 BODY[]; ADR-0802 keeps the stale case off the ratchet
