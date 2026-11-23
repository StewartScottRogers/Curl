---
id: BL-5821
title: Fix fitness finding STD-173: Linux LDAP output base64-encodes values with an embedded TAB, CR or LF; curl (OpenLDAP) prints them raw
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapEntryFormatter.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5821 — Fix fitness finding STD-173: Linux LDAP output base64-encodes values with an embedded TAB, CR or LF; curl (OpenLDAP) prints them raw

## Goal

STD-173 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Linux LDAP output base64-encodes values with an embedded TAB, CR or LF; curl (OpenLDAP) prints them raw. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (output format)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** LDAP entry formatting, Linux build
- **Repro:** a mock LDAP server on 22513 returning an entry with `description` values `a<TAB>b`, `a<CR>b`, `nl<LF>`, plus values with a leading TAB, trailing space, 0x7f and NUL. On Linux: `curl -sS -u cn=x,dc=example,dc=org:pw ldap://127.0.0.1:22513/dc=example,dc=org`
- **Expected (REF2):** `\tdescription: a<TAB>b`, `\tdescription: a<CR>b`, `\tdescription: nl<LF>` printed raw; the leading-TAB, trailing-space, 0x7f and NUL values base64 (`::`).
- **Actual (.NET):** `description:: YQli`, `description:: YQ1i`, `description:: bmwK`; the other values match. This is the Windows (WinLDAP) rule applied on Linux.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapEntryFormatter.cs` `IsPrintable` / `IsPrintableAscii`: in the OpenLDAP dialect treat interior TAB/CR/LF as printable.
- **Acceptance check:** the Linux output is byte-identical to REF2's for the command above.

## Acceptance criteria

- [ ] the Linux output is byte-identical to REF2's for the command above.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: af3c65dc 69116afe 3d836d5e.
