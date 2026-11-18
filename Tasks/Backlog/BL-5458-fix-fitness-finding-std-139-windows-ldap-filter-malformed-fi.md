---
id: BL-5458
title: Fix fitness finding STD-139: Windows LDAP filter: malformed filters that curl rejects with "Filter Error" are sent
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5458 — Fix fitness finding STD-139: Windows LDAP filter: malformed filters that curl rejects with "Filter Error" are sent

## Goal

STD-139 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows LDAP filter: malformed filters that curl rejects with "Filter Error" are sent. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (3 shapes, 2 runs)
- **Protocol/area:** LDAP filter, WinLDAP dialect, Windows
- **Repro:** the server and relay from STD-134. `curl -sS -u cn=admin,dc=example,dc=org:admin "ldap://127.0.0.1:42880/ou=people,dc=example,dc=org?cn?sub?(cn=**)"`; also `(cn:=)` and `(cn:dn:=)`.
- **Expected (REF):** `curl: (39) LDAP remote: Filter Error`, exit 39, no search sent.
- **Actual (PORT):** sends substrings with an empty sequence (`a4840000000a0402636e308400000000`) or an extensible match with an empty value (`…8300840100`, `…83008401ff`); exit 0, no output. `(cn=a**)` and `(cn=**a)` match REF.
- **Likely location:** `LdapFilterEncoder.cs`.
- **Acceptance check:** each command exits 39 with `curl: (39) LDAP remote: Filter Error`.

## Acceptance criteria

- [ ] each command exits 39 with `curl: (39) LDAP remote: Filter Error`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-139 (Low).