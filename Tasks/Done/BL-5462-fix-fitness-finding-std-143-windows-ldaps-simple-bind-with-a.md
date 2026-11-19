---
id: BL-5462
title: Fix fitness finding STD-143: Windows ldaps:// simple bind with a wrong password says "Protocol Error"; curl says "Invalid Credentials"
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5461]
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapBind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5462 — Fix fitness finding STD-143: Windows ldaps:// simple bind with a wrong password says "Protocol Error"; curl says "Invalid Credentials"

## Goal

STD-143 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows ldaps:// simple bind with a wrong password says "Protocol Error"; curl says "Invalid Credentials". Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (stderr text only; exit 38 on both)
- **Confidence:** Confirmed (2 users, 2 runs)
- **Protocol/area:** LDAPS simple bind, Windows
- **Repro:** the container from STD-142. `curl -sS -m 15 -k -u cn=admin,dc=example,dc=org:wrong "ldaps://127.0.0.1:42636/ou=people,dc=example,dc=org?cn?sub?(uid=alice)"`; also `-u cn=nobody,dc=example,dc=org:x`.
- **Expected (REF):** `curl: (38) LDAP local: bind via ldap_win_bind Invalid Credentials`. (On plain `ldap://`, REF retries LDAPv2 after result 49 and reports `Protocol Error`, which the PORT matches.)
- **Actual (PORT):** `curl: (38) LDAP local: bind via ldap_win_bind Protocol Error` — the plaintext v2-retry behaviour is applied over ldaps too.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapBind.cs` (v3→v2 retry) and its ldaps caller in `LdapProtocolHandler.cs`.
- **Acceptance check:** the command prints `… Invalid Credentials`, exit 38; plain `ldap://` still prints `Protocol Error`.

## Acceptance criteria

- [x] the command prints `… Invalid Credentials`, exit 38; plain `ldap://` still prints `Protocol Error`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: LdapBind.BindAsync gets a flag for ldaps (context.Url.Scheme == TlsScheme, passed from BindWithCredentialsAsync in LdapProtocolHandler.cs); WinLDAP over ldaps binds LDAPv3 once, no v2 retry, unbinds and fails with the first reply's result text. Handler file is held by BL-5461, so touches widened and depends-on BL-5461.

- Done: over ldaps:// the WinLDAP simple bind binds LDAPv3 once (no LDAPv2 retry), unbinds and fails with the first result's text, so a wrong password gives exit 38 `Invalid Credentials`; plain ldap:// keeps the v3-then-v2 sequence (its tests, e.g. Cgr234 and the RetryRefused cases, still pass). ADR-1304 records it. The retry moved to `LdapBind.RetryAsLdapV2Async` to keep `BindAsWinLdapAsync` within the complexity gate (Measure-CodeQuality: 100/100, 0 failing).
- Criterion 1 checked by `LdapBindOverTlsTests` replaying the exchange through the handler (ldaps URL, result 49 -> exit 38 Invalid Credentials, bytes BindV3 + Unbind); no LDAPS container is reachable from a lane, so the reference text is the finding's (confirmed by 2 users, 2 runs). Platform-neutral: no paths or OS text.
- Full build clean (0 warnings); fast tests green across 115 test assemblies.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-143 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 530f7f63 69116afe 3d836d5e.
- 2026-11-18: Doing -> Backlog. Needs LdapProtocolHandler.cs, held by BL-5461 (Doing); depends-on BL-5461
- 2026-11-18: Backlog -> Doing. Criteria at claim: 530f7f63 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. Windows ldaps:// simple bind with a wrong password fails with exit 38 Invalid Credentials, no LDAPv2 retry
