---
id: BL-525
title: Decide how NTLM and Negotiate are answered on the base class library
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-525 — Decide how NTLM and Negotiate are answered on the base class library

## Goal

An ADR decides how Curl produces NTLM and Negotiate (SPNEGO/Kerberos) tokens on Windows, Linux and macOS with the base class library only, so HTTP (`--ntlm`, `--negotiate`, `--anyauth`), proxies (`--proxy-ntlm`, `--proxy-negotiate`), SOCKS5 GSS-API, SASL `NTLM`/`GSSAPI` and SMB can all be built on it.

## Context

- Prerequisite of audit rows 14 (proxy auth), 16 (`--socks5-gssapi`), 23 (`--krb`, `--delegation`, `--service-name`), 34 (SASL NTLM/GSSAPI) and 39 (SMB uses NTLM). Not an audit row itself: ADR-0028 records that `--ntlm` and `--negotiate` parse but nothing answers them ("Curl sends nothing until NTLM is built"), and `Curl.Authentication.UnitLibrary/BasicAndBearerAuthenticator.cs` says so.
- Candidates: `System.Net.Security.NegotiateAuthentication` (BCL, .NET 7+; SSPI on Windows, GSSAPI on Linux and macOS, where NTLM needs the system's gss-ntlmssp), or a hand-written NTLMv2 (needs MD4, which the BCL does not have, and HMAC-MD5, which it does). Measure what each gives on each platform before deciding; tests must stay off the network, so the decision includes the seam that lets tests inject tokens.
- Standing rule: base class library only; if a platform cannot do it on the BCL, the ADR says what Curl then prints (matching a curl build without that feature) rather than adding a package.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with alternatives weighed and each platform's behaviour stated (what works, what is refused and how).
- [ ] It names the seam (an interface in `Curl.Authentication.UnitLibrary` or `Curl.Protocol.Abstractions.UnitLibrary`) that BL-526, BL-527 and later SASL, proxy and SMB tasks use, and how unit tests fake it.
- [ ] ADR-0028's "until NTLM is built" consequence is cross-referenced from the new ADR.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
