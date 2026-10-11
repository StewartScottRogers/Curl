---
id: BL-2020
title: Close the GF-0001 cases left from BL-1997: test795 hangs on an HTTP redirect to IMAP, test2043 needs revoked.badssl.com
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2020 — Close the GF-0001 cases left from BL-1997: test795 hangs on an HTTP redirect to IMAP, test2043 needs revoked.badssl.com

## Goal

Upstream cases 795 and 2043, the last two GF-0001 items BL-1997 left, pass through Curl in process, so a later gap analysis measures `behaviour:test795` and `behaviour:test2043` as `match`.

## Context

BL-1997 made the other 34 GF-0001 cases pass the ratchet (`Curl.Conformance.UnitTests/PassingUpstreamCases.txt`). Left:

- test795 (HTTP with credentials redirects to IMAP: `-u user:secret --location --proto-redir imap --resolve host:%IMAPPORT:%HOSTIP`): in the ratchet curl does not finish within 20 seconds. The expected IMAP exchange is `B001 CAPABILITY`, `B002 AUTHENTICATE PLAIN <base64 of \0v\0>` (SASL-IR, the redirect URL's user `v` with an empty password, not `-u`'s), `B003 LIST "7950002" *`, `B004 LOGOUT`. Find whether the hang is Curl's (the IMAP handler after a cross-scheme redirect, or the redirect's connect to the IMAP stand-in through `--resolve`) or the harness's (the http and imap stand-ins in one case), and fix that side.
- test2043 (`--ssl-no-revoke -I https://revoked.badssl.com/`, Schannel, expects exit 0): the ratchet skips it because it reaches the internet; the gap office measured exit 52. Decide in an ADR how the case is measured in process (for example an HTTPS stand-in answering for revoked.badssl.com with a certificate whose revocation status is unknown), or why it is measured as excluded, so the gap item can close.

## Acceptance criteria

- [x] `behaviour:test795`: upstream test795 passes in the ratchet and is listed in `PassingUpstreamCases.txt`.
- [x] `behaviour:test2043`: upstream test2043 passes in process, or an ADR records why the gap office measures it as `excluded`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- test795 no longer hangs: on this branch the ratchet reported it Inconclusive with "test795 passes; add 795 to PassingUpstreamCases.txt" in 52 ms. Work that landed after BL-1997 fixed it, most likely BL-1987 (IMAP keeps the connection and tags by connection number) or BL-2011 (mail stand-in relay). No code change was needed; 795 is now listed.
- test2043: ADR-0473 decides it stays skipped in the ratchet and is measured `excluded` by the gap office. It checks a live internet host's revocation status, which no in-process stand-in reproduces without the harness faking the trust store, and `--ssl-no-revoke` is already pinned by unit tests in Curl.Console.UnitTests and Curl.Networking.UnitTests.
- Applying the exclusion in the gap office's measurement is under `Gap/`. The audit guard refuses a lane filing a task that touches it, so an interactive session's next gap run applies ADR-0473.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. test795 is listed in the passing ratchet; ADR-0473 measures test2043 excluded
