---
id: BL-5457
title: Fix fitness finding STD-138: Windows LDAP filter: a one-digit hex escape (\2, \4) is sent literally; WinLDAP decodes it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapHexEscape.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5457 — Fix fitness finding STD-138: Windows LDAP filter: a one-digit hex escape (\2, \4) is sent literally; WinLDAP decodes it

## Goal

STD-138 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows LDAP filter: a one-digit hex escape (\2, \4) is sent literally; WinLDAP decodes it. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (3 shapes, 2 runs)
- **Protocol/area:** LDAP filter escapes, WinLDAP dialect, Windows
- **Repro:** the server and relay from STD-134. `curl -sS -u cn=admin,dc=example,dc=org:admin "ldap://127.0.0.1:42880/ou=people,dc=example,dc=org?cn?sub?(cn=a\2)"`; also `(cn=a\2g)` and `(cn=\4)`.
- **Expected (REF):** values `a\x02` (`04026102`), `a\x02g` (`0403610267`), `\x04` (`040104`).
- **Actual (PORT):** values `a\2` (`0403615c32`), `a\2g`, `\4`; exit 0 in both. Two-digit escapes already match.
- **Likely location:** `LdapFilterEncoder.cs` / `LdapHexEscape.cs`.
- **Acceptance check:** the first command's filter ends in `04026102`.

## Acceptance criteria

- [ ] the first command's filter ends in `04026102`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-138 (Low).