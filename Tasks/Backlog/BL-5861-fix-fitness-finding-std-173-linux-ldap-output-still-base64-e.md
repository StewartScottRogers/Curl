---
id: BL-5861
title: Fix fitness finding STD-173: Linux LDAP output still base64-encodes values that start with :, <, CR or LF, or contain VT or FF; OpenLDAP curl prints them raw
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapEntryFormatter.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5861 — Fix fitness finding STD-173: Linux LDAP output still base64-encodes values that start with :, <, CR or LF, or contain VT or FF; OpenLDAP curl prints them raw

## Goal

STD-173 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: Linux LDAP output still base64-encodes values that start with :, <, CR or LF, or contain VT or FF; OpenLDAP curl prints them raw. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

STD-173 was filed before as BL-5821 (Done); this review found it only partly fixed, so this task finishes it.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Low (output format) · **Confidence:** Confirmed (2 runs) · **Area:** LDAP entry formatting, Linux build. Interior TAB/CR/LF are now raw (fixed part).
- **Setup:** a mock LDAP server on 22513 returning entry `cn=ctl2,dc=example,dc=org` whose `description` values are `:colon`, `<lt`, `x<VT>y`, `x<FF>y`, `<CR>lead`, `<LF>lead`.
- **Repro (Linux):** `curl -sS -u cn=x,dc=example,dc=org:pw ldap://127.0.0.1:22513/dc=example,dc=org`
- **Expected (REF2):** raw lines `\tdescription: :colon`, `\tdescription: <lt`, `\tdescription: x\vy`, `\tdescription: x\fy`, `\tdescription: \rlead`, `\tdescription: \nlead` (curl's openldap.c base64-encodes only when the first or last byte is space/TAB, or a byte is neither ISPRINT nor ISSPACE).
- **Actual (.NET Linux):** `description:: OmNvbG9u`, `PGx0`, `eAt5`, `eAx5`, `DWxlYWQ=`, `CmxlYWQ=`. Windows output is byte-identical to REF.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapEntryFormatter.cs`: `StartsUnsafe` / `UnsafeFirstOctets` (`0x3A, 0x3C, 0x0A, 0x0D`) apply in both dialects; `IsInteriorWhitespaceControl` covers only 0x09/0x0D/0x0A. In the OpenLDAP dialect only a leading blank is unsafe and 0x0B/0x0C count as whitespace.
- **Acceptance check:** the repro output is byte-identical to REF2.

## Acceptance criteria

- [ ] the repro output is byte-identical to REF2.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.