---
id: BL-5459
title: Fix fitness finding STD-140: Windows LDAP filter (attr:rule:dn:=v) (dn after the rule) loses dnAttributes; WinLDAP sets it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5459 — Fix fitness finding STD-140: Windows LDAP filter (attr:rule:dn:=v) (dn after the rule) loses dnAttributes; WinLDAP sets it

## Goal

STD-140 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows LDAP filter (attr:rule:dn:=v) (dn after the rule) loses dnAttributes; WinLDAP sets it. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (wire difference; results can differ for DN-component matches)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** LDAP filter, WinLDAP dialect, Windows
- **Repro:** the server and relay from STD-134. `curl -sS -u cn=admin,dc=example,dc=org:admin "ldap://127.0.0.1:42880/ou=people,dc=example,dc=org?cn?sub?(cn:caseExactMatch:dn:=Alice%20Example)"`
- **Expected (REF):** `a984000000168202636e830d416c696365204578616d706c658401ff` (dnAttributes TRUE).
- **Actual (PORT):** ends in `840100` (dnAttributes FALSE). Both print the entry, exit 0.
- **Likely location:** `LdapFilterEncoder.cs`, extensible-match parse.
- **Acceptance check:** the filter bytes end in `8401ff`.

## Acceptance criteria

- [ ] the filter bytes end in `8401ff`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-140 (Low).