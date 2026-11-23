---
id: BL-5818
title: Fix fitness finding STD-170: Windows LDAP filter: approximate, >= and <= comparisons with an empty value are sent; curl rejects them
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs]
requirement: none
created: 2026-11-23
completed: 2026-11-23
---
# BL-5818 — Fix fitness finding STD-170: Windows LDAP filter: approximate, >= and <= comparisons with an empty value are sent; curl rejects them

## Goal

STD-170 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Windows LDAP filter: approximate, >= and <= comparisons with an empty value are sent; curl rejects them. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (silent wrong operation: a different search is sent and the run exits 0)
- **Confidence:** Confirmed (5 shapes, 2 runs)
- **Protocol/area:** LDAP filter, WinLDAP dialect, Windows build
- **Repro:** the OpenLDAP server from STD-117 on 127.0.0.1:22389 with a logging relay on 22880. `curl -sS -u cn=admin,dc=example,dc=org:admin "ldap://127.0.0.1:22880/ou=people,dc=example,dc=org?cn?sub?(cn<=)"`; also `(cn~=)`, `(cn>=)`, `(cn~=%20)` and `(%26(cn<=)(uid=alice))`.
- **Expected (REF):** `curl: (39) LDAP remote: Filter Error`, exit 39, no SearchRequest sent. (`(cn=)` already matches REF.)
- **Actual (.NET):** sends a SearchRequest with filter `a6 84 00 00 00 06 04 02 63 6e 04 00` (`~=`: `a8 84 …`; `>=`: `a5 84 …`) holding an empty value; exit 0, no output.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs`, WinLDAP dialect: the empty-value rejection covers `=` and extensible match but not `~=`, `>=`, `<=`.
- **Acceptance check:** the first command exits 39 with `curl: (39) LDAP remote: Filter Error` and the relay log shows no SearchRequest.

## Acceptance criteria

- [x] the first command exits 39 with `curl: (39) LDAP remote: Filter Error` and the relay log shows no SearchRequest.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- WinLDAP now refuses an empty value for ~=, >= and <= like it does for =; libldap still sends it. Measure-CodeQuality clean for the Ldap library.

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: cc1cf009 69116afe 3d836d5e.
- 2026-11-23: Doing -> Done. WinLDAP refuses empty ~=, >= and <= values
