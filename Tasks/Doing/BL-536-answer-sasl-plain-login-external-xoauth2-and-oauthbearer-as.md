---
id: BL-536
title: Answer SASL PLAIN, LOGIN, EXTERNAL, XOAUTH2 and OAUTHBEARER as curl picks them
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-534]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-536 — Answer SASL PLAIN, LOGIN, EXTERNAL, XOAUTH2 and OAUTHBEARER as curl picks them

## Goal

`Curl.Authentication.UnitLibrary` implements the SASL authenticator contract from BL-534: it picks a mechanism from the server's list in curl 8.21.0's preference order (restricted by `--login-options AUTH=<mech>`), and produces the initial response and each continuation for PLAIN, LOGIN, EXTERNAL, XOAUTH2 (`--oauth2-bearer`) and OAUTHBEARER, honouring `--sasl-authzid` and `--sasl-ir`.

## Context

- Conformance audit 2026-09-28, rows 23 and 34. Contract and preference order: BL-533's ADR and BL-534.
- RFC 4616 (PLAIN), draft-murchison-sasl-login (LOGIN), RFC 4422 (EXTERNAL), RFC 7628 (OAUTHBEARER), Google's XOAUTH2 description. CRAM-MD5 and DIGEST-MD5 are BL-537, NTLM and GSSAPI BL-538: until they land, the ranking treats them as not offered, and the XML doc says so.
- Base64 via `System.Convert`. No network: the authenticator is pure.

## Acceptance criteria

- [ ] `Curl.Authentication.UnitTests` pin each mechanism's messages byte for byte for a fixed user, password, authzid and bearer token, with and without an initial response.
- [ ] Mechanism choice is pinned for the server lists recorded in BL-533's ADR, and for `--login-options AUTH=LOGIN`, `AUTH=*` and an unavailable `AUTH=` choice (curl's measured outcome, from the ADR or measured here with `Record-CurlExchange.ps1 -Smtp`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
