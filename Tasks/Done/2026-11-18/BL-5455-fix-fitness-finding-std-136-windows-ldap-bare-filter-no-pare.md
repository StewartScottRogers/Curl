---
id: BL-5455
title: Fix fitness finding STD-136: Windows LDAP bare filter (no parentheses) with leading spaces sends attribute  uid; curl trims and finds the entry
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5455 — Fix fitness finding STD-136: Windows LDAP bare filter (no parentheses) with leading spaces sends attribute  uid; curl trims and finds the entry

## Goal

STD-136 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows LDAP bare filter (no parentheses) with leading spaces sends attribute  uid; curl trims and finds the entry. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (silent wrong result: empty output, exit 0)
- **Confidence:** Confirmed (2 shapes, 2 runs)
- **Protocol/area:** LDAP filter, WinLDAP dialect, Windows (neighbour of the STD-106 fix)
- **Repro:** the server and relay from STD-134. `curl -sS -u cn=admin,dc=example,dc=org:admin "ldap://127.0.0.1:42880/ou=people,dc=example,dc=org?cn?sub?%20uid=alice"`; also `?cn?sub?%20%20cn=user001` (with a `cn=user001` entry).
- **Expected (REF):** filter `a3840000000c04037569640405616c696365`; prints `DN: uid=alice,ou=people,dc=example,dc=org` and `cn: Alice Example`, exit 0.
- **Actual (PORT):** filter `a3840000000d0404207569640405616c696365` (attribute ` uid`); no output, exit 0.
- **Likely location:** `LdapFilterEncoder.cs`, the bare `attr=value` path (the STD-106 trim covers only the parenthesised form).
- **Acceptance check:** the command prints alice's entry and its filter bytes equal REF's.

## Acceptance criteria

- [x] the command prints alice's entry and its filter bytes equal REF's.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Fixed in LdapFilterEncoder.Encode: WinLDAP bare filters now skip leading spaces like the parenthesised form. Test replaces the old one that pinned spaces in the attribute. Quality run: 0 failing members. Filter bytes pinned to REF hex A3840000000C04037569640405616C696365.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-136 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 7023c4cf 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. WinLDAP bare filter trims leading spaces; unit test pins REF bytes
