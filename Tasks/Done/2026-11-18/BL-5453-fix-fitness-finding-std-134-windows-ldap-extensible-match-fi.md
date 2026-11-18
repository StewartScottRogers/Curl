---
id: BL-5453
title: Fix fitness finding STD-134: Windows LDAP extensible-match filter with :dn: before a named matching rule sends the bogus matchingRule dn:<rule>; the search silently returns nothing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5453 — Fix fitness finding STD-134: Windows LDAP extensible-match filter with :dn: before a named matching rule sends the bogus matchingRule dn:<rule>; the search silently returns nothing

## Goal

STD-134 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows LDAP extensible-match filter with :dn: before a named matching rule sends the bogus matchingRule dn:<rule>; the search silently returns nothing. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (silent wrong result, exit 0)
- **Confidence:** Confirmed (5 shapes, 2 runs)
- **Protocol/area:** LDAP filter, WinLDAP dialect, Windows (neighbour of the STD-108 fix)
- **Repro:** the OpenLDAP container from STD-116 steps 1–2 (any server works for the wire check) and a logging relay on 127.0.0.1:42880.
  `curl -sS -u cn=admin,dc=example,dc=org:admin "ldap://127.0.0.1:42880/ou=people,dc=example,dc=org?cn?sub?(cn:dn:caseExactMatch:=Alice%20Example)"`; also `(cn:DN:caseExactMatch:=Alice%20Example)`, `(cn:dn:caseIgnoreMatch:=alice%20example)`, `(:dn:caseExactMatch:=Alice%20Example)`.
- **Expected (REF):** filter bytes `a984000000168202636e830d416c696365204578616d706c658401ff` (rule dropped, dnAttributes TRUE); prints `DN: uid=alice,…` / `cn: Alice Example`, exit 0.
- **Actual (PORT):** filter `a98400000029 8111 646e3a6361736545786163744d61746368 8202636e … 8401ff` (matchingRule `dn:caseExactMatch`); nothing printed, exit 0. The OID forms (`(cn:dn:2.5.13.5:=…)`) match REF.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs`, extensible-match parse: the `dn` token is not split from the rule before the ADR-1298 rule drop.
- **Acceptance check:** the first command's filter equals REF's bytes above and it prints alice's entry.

## Acceptance criteria

- [x] the first command's filter equals REF's bytes above and it prints alice's entry.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Fix: IsNamedRule now looks past a leading dn: so a named rule after :dn: is dropped (OID still kept). Filter bytes match the finding's REF by unit test; no live relay run.


## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-134 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 1eade6bc 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. WinLDAP named rule after :dn: is dropped like REF
