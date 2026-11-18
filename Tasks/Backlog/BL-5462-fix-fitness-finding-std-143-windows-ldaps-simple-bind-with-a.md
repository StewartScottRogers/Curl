---
id: BL-5462
title: Fix fitness finding STD-143: Windows ldaps:// simple bind with a wrong password says "Protocol Error"; curl says "Invalid Credentials"
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapBind.cs]
requirement: none
created: 2026-11-18
completed:
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

- [ ] the command prints `… Invalid Credentials`, exit 38; plain `ldap://` still prints `Protocol Error`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-143 (Low).