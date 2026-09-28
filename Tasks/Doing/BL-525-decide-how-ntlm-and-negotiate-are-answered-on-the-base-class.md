---
id: BL-525
title: Decide how NTLM, Negotiate, Kerberos and GSS-API work on every platform
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-525 — Decide how NTLM, Negotiate, Kerberos and GSS-API work on every platform

## Goal

An ADR decides how Curl produces NTLM, Negotiate (SPNEGO), Kerberos and GSS-API tokens on Windows, Linux and macOS, so that HTTP (`--ntlm`, `--negotiate`, `--anyauth`), proxies (`--proxy-ntlm`, `--proxy-negotiate`), SOCKS5 GSS-API, SASL `NTLM`/`GSSAPI`, FTP `--krb` and SMB all work on every platform: which parts are hand-built (in `Curl.Ntlm.UnitLibrary` and `Curl.Kerberos.UnitLibrary`, SPNEGO in `Curl.Authentication.UnitLibrary`) and where, if anywhere, the BCL's `NegotiateAuthentication` is used instead.

## Context

- Prerequisite of audit rows 14 (proxy auth), 16 (`--socks5-gssapi`), 23 (`--krb`, `--delegation`, `--service-name`), 34 (SASL NTLM/GSSAPI) and 39 (SMB uses NTLM). Not an audit row itself: ADR-0028 records that `--ntlm` and `--negotiate` parse but nothing answers them ("Curl sends nothing until NTLM is built"), and `Curl.Authentication.UnitLibrary/BasicAndBearerAuthenticator.cs` says so.
- Standing rule (root `CLAUDE.md`, "Decisions", Stewart 2026-09-28): do what a complete reimplementation of curl needs; nothing is refused because it is hard or because the BCL lacks it; each hand-built piece lives in its own library; never a package. So this ADR decides HOW each mechanism works on each platform, never WHETHER: no platform answers "not supported".
- The BCL's `System.Net.Security.NegotiateAuthentication` uses SSPI on Windows (the logged-on user's Kerberos and NTLM credentials, which only SSPI can reach) and the system's GSS-API library on Linux and macOS (absent on many machines; NTLM there also needs `gss-ntlmssp`). Hand-built pieces already on the board: NTLM in `Curl.Ntlm.UnitLibrary` (BL-682 to BL-684, MD4 from BL-675); Kerberos V5 with its credential cache, `krb5.conf`, KDC exchanges and the GSS-API Kerberos mechanism in `Curl.Kerberos.UnitLibrary` (BL-685 to BL-691); SPNEGO in `Curl.Authentication.UnitLibrary` (BL-692). Decide per platform and per case (default credentials vs `-u user:password`) which route is used, so that each works everywhere and matches what the platform's curl build does where both do the same thing.
- Measure first with `Record-CurlExchange.ps1`: `--ntlm -u u:p` and `--negotiate -u :` against a `401` on Windows and on Linux or macOS (bytes, stderr, exit), and `NegotiateAuthentication`'s behaviour on each CI platform with and without a system GSS-API library.
- Tests must stay off the network: the ADR names the token-source seam that lets tests inject tokens, and the KDC transport and SRV-lookup seams of BL-689/BL-690 with the production adapters `Curl.Networking.UnitLibrary` provides (composed in BL-527).

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with the measurements, alternatives weighed, and a table per platform and credential case stating which implementation answers NTLM, Negotiate/Kerberos and GSS-API; no cell says "refused" or "not offered".
- [ ] It names the seam (an interface in `Curl.Authentication.UnitLibrary` or `Curl.Protocol.Abstractions.UnitLibrary`) that BL-526, BL-527, BL-538, BL-604, BL-615, BL-693 and the SMB tasks use, and how unit tests fake it, and confirms the library split above (or records why it differs and amends BL-667's ADR list).
- [ ] ADR-0028's "until NTLM is built" consequence is cross-referenced from the new ADR.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
