---
id: BL-5454
title: Fix fitness finding STD-135: Windows LDAP filter: leading spaces in an assertion value are sent; WinLDAP strips them
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5454 — Fix fitness finding STD-135: Windows LDAP filter: leading spaces in an assertion value are sent; WinLDAP strips them

## Goal

STD-135 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows LDAP filter: leading spaces in an assertion value are sent; WinLDAP strips them. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (wire difference; results differ for space-significant matching rules)
- **Confidence:** Confirmed (8 shapes, 2 runs)
- **Protocol/area:** LDAP filter, WinLDAP dialect, Windows
- **Repro:** the server and relay from STD-134. `curl -sS -u cn=admin,dc=example,dc=org:admin "ldap://127.0.0.1:42880/ou=people,dc=example,dc=org?cn?sub?(uid=%20alice)"`; also `(uid=%20%20alice)`, `(%26(uid=%20alice)(sn=Example))`, `(cn=%20Alice*)`, `(cn~=%20Alise)`, `(cn>=%20user050)`, `(description=%20hello%20world)`.
- **Expected (REF):** equality filter `a3840000000c04037569640405616c696365` (value `alice`); the substring initial is `Alice` (`8005416c696365`).
- **Actual (PORT):** `a3840000000d0403756964040620616c696365` (value ` alice`), and likewise for the others; exit 0. (Stdout is the same against OpenLDAP because its rules ignore insignificant spaces.) Trailing spaces already match REF.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs` (WinLDAP dialect), value parse after `=`, `~=`, `>=`, `<=`.
- **Acceptance check:** the first command's filter bytes equal `a3840000000c04037569640405616c696365`.

## Acceptance criteria

- [x] the first command's filter bytes equal `a3840000000c04037569640405616c696365`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

WinLDAP: LdapFilterEncoder.ValueAfterEquals drops leading spaces after =, ~=, >=, <= (not extensible matches); libldap keeps them. Measure-CodeQuality for the Ldap library: 0 failing members.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-135 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 4c1d41f6 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. Fixed: WinLDAP drops leading spaces in assertion values
