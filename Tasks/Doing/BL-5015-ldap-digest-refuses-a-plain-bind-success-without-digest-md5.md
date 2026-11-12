---
id: BL-5015
title: LDAP --digest refuses a plain bind success without DIGEST-MD5 with exit 38
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapAuthenticatedBind.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapDigestMd5BindTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5015 — LDAP --digest refuses a plain bind success without DIGEST-MD5 with exit 38

## Goal

`curl ldap://... -u ... --digest` refuses a server that answers the first bind with plain success instead of a DIGEST-MD5 challenge: exit 38, no search sent, nothing on stdout.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-036 (Low, Confirmed).

Weakens security (minor): the login succeeds without the mechanism running. No password is sent.

Repro:
1. Run an LDAP mock on 127.0.0.1:45001. It answers every BindRequest with a BindResponse of resultCode 0 and no serverSaslCreds, and every SearchRequest with one entry, `cn=a`.
2. `curl -sS "ldap://127.0.0.1:45001/dc=x" -u "cn=admin,dc=x:pw" --digest`

Expected (REF): exit 38, `curl: (38) LDAP local: bind via ldap_win_bind Protocol Error`. No search output.

Actual (PORT): exit 0. It prints `DN: cn=a ...` and never answers a DIGEST-MD5 challenge.

Where to start: `Curl.Original.Protocol.Ldap.UnitLibrary/LdapAuthenticatedBind.cs`, `DigestAsync`. It returns `initial` when the first reply is not `SaslBindInProgress`, and `BindAsync` then treats `reply.IsSuccess` as bound.

## Acceptance criteria

- [ ] A unit test in `Curl.Original.Protocol.Ldap.UnitTests/LdapDigestMd5BindTests.cs` (mock answering BindResponse resultCode 0 with no serverSaslCreds to the first DIGEST-MD5 bind) shows exit `CurlExitCode` 38 with message `LDAP local: bind via ldap_win_bind Protocol Error`, no SearchRequest sent, and nothing written to stdout.
- [ ] Regression: in the same project, a real DIGEST-MD5 exchange (challenge, response, success) still binds, and `--basic` against the same always-success mock still binds and returns the entry (exit 0).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. The expected message is REF's Windows (Schannel build) text; measure the Linux/OpenSSL build's answer for the same mock and pin each platform's text in its own test if they differ.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 9b350057 0cc9e8ee a0b49a29.
