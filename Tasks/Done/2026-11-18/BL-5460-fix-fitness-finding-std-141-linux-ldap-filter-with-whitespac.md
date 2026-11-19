---
id: BL-5460
title: Fix fitness finding STD-141: Linux LDAP filter with whitespace after the closing parenthesis is accepted; libldap rejects it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs, Curl.Original.Protocol.Ldap.UnitLibrary/OpenLdapUrlReader.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5460 — Fix fitness finding STD-141: Linux LDAP filter with whitespace after the closing parenthesis is accepted; libldap rejects it

## Goal

STD-141 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Linux LDAP filter with whitespace after the closing parenthesis is accepted; libldap rejects it. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 shapes, 2 runs)
- **Protocol/area:** LDAP filter, OpenLDAP dialect, Linux build
- **Repro:** the OpenLDAP container from STD-116 steps 1–2 on 127.0.0.1:42389; from Linux/WSL: `curl -sS -u cn=admin,dc=example,dc=org:admin 'ldap://127.0.0.1:42389/ou=people,dc=example,dc=org?cn?sub?(uid=alice)%20'`; also `(cn=user0*)%20`.
- **Expected (REF2):** `curl: (39) LDAP local: ldap_search_ext Bad search filter`, exit 39.
- **Actual (PORT, Linux):** the search runs, prints the entries, exit 0.
- **Likely location:** `LdapFilterEncoder.cs` (OpenLDAP dialect) or `OpenLdapUrlReader.cs`.
- **Acceptance check:** the command exits 39 with `curl: (39) LDAP local: ldap_search_ext Bad search filter`.

## Acceptance criteria

- [x] the command exits 39 with `curl: (39) LDAP local: ldap_search_ext Bad search filter`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

libldap takes no text after its one top-level item, blanks included; `ParseTopLevel` no longer skips trailing blanks for OpenLDAP (WinLDAP still does). Pinned in LdapFilterEncoderTests. Linux/macOS CI: change is platform-neutral.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-141 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: d3b2b661 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. Linux LDAP filter with trailing blanks now refused
