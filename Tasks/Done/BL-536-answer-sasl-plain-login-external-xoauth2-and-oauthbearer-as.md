---
id: BL-536
title: Answer SASL PLAIN, LOGIN, EXTERNAL, XOAUTH2 and OAUTHBEARER as curl picks them
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-534]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-536 — Answer SASL PLAIN, LOGIN, EXTERNAL, XOAUTH2 and OAUTHBEARER as curl picks them

## Goal

`Curl.Authentication.UnitLibrary` implements the SASL authenticator contract from BL-534: it picks a mechanism from the server's list in curl 8.21.0's preference order (restricted by `--login-options AUTH=<mech>`), and produces the initial response and each continuation for PLAIN, LOGIN, EXTERNAL, XOAUTH2 (`--oauth2-bearer`) and OAUTHBEARER, honouring `--sasl-authzid` and `--sasl-ir`.

## Context

- Conformance audit 2026-09-28, rows 23 and 34. Contract and preference order: BL-533's ADR and BL-534.
- RFC 4616 (PLAIN), draft-murchison-sasl-login (LOGIN), RFC 4422 (EXTERNAL), RFC 7628 (OAUTHBEARER), Google's XOAUTH2 description. CRAM-MD5 and DIGEST-MD5 are BL-537, NTLM and GSSAPI BL-538: until they land, the ranking treats them as not offered, and the XML doc says so.
- Base64 via `System.Convert`. No network: the authenticator is pure.

## Acceptance criteria

- [x] `Curl.Authentication.UnitTests` pin each mechanism's messages byte for byte for a fixed user, password, authzid and bearer token, with and without an initial response.
- [x] Mechanism choice is pinned for the server lists recorded in BL-533's ADR, and for `--login-options AUTH=LOGIN`, `AUTH=*` and an unavailable `AUTH=` choice (curl's measured outcome, from the ADR or measured here with `Record-CurlExchange.ps1 -Smtp`).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: `SaslAuthenticator` (public, `ISaslAuthenticator`, takes the `CredentialEncoding` like the HTTP authenticator), `SaslMechanismRanking` (curl's order as a table of name + usability predicate; GSSAPI, DIGEST-MD5, CRAM-MD5 and NTLM hold their places with a never-usable predicate until BL-537/BL-538), `ScriptedSaslExchange` (initial response, then a fixed answer list; `null` once it runs out). Tests: `SaslAuthenticatorTests`, 45 cases.
- Measured curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1 -Smtp` on 2026-09-28; the table is in ADR-0123 (Decided by Claude under Stewart's delegation). Findings that differ from ADR-0121: PLAIN outranks LOGIN; `--sasl-authzid` does not skip LOGIN; a bearer token excludes PLAIN and LOGIN; `--sasl-ir` LOGIN sends the user as its initial response (`AUTH LOGIN dQ==`), so every built mechanism has an initial response, sent inline or in answer to the first challenge. ADR-0121 §2 carries an amendment note.
- OAUTHBEARER: curl always sends `port=<p>`; `SaslRequest` has no port and `Curl.Protocol.Abstractions.UnitLibrary` is held by BL-561, so `Begin` leaves `port=` out for now and `OAuthBearerMessage` is pinned with the port. Filed BL-751 to add the port and fix the Abstractions doc (LOGIN's initial response).
- Also found for the SMTP handler (recorded in ADR-0123): when `AUTH` is the last line of the EHLO reply, curl ignores its last mechanism.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0123, its README row and the ADR-0121 amendment note; no task in Doing names it.
- `AUTH=BOGUS`: curl exits 3 before connecting (the parser's job); the authenticator answers `null`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. SaslAuthenticator picks among PLAIN, LOGIN, EXTERNAL, XOAUTH2 and OAUTHBEARER in curl's measured order and sends curl's bytes
