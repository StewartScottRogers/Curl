---
id: BL-5820
title: Fix fitness finding STD-172: Linux ldap:// with --ssl-reqd: a StartTLS certificate failure exits 60; curl exits 64
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapStartTls.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5820 — Fix fitness finding STD-172: Linux ldap:// with --ssl-reqd: a StartTLS certificate failure exits 60; curl exits 64

## Goal

STD-172 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Linux ldap:// with --ssl-reqd: a StartTLS certificate failure exits 60; curl exits 64. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (no credentials are sent)
- **Confidence:** Confirmed (2 shapes, 2 runs)
- **Protocol/area:** LDAP StartTLS, Linux build
- **Repro:** the osixia OpenLDAP container from STD-117 (its default self-signed certificate has no IP SAN). On Linux: `curl -sS -m 10 --ssl-reqd -u cn=admin,dc=example,dc=org:admin 'ldap://127.0.0.1:22389/ou=people,dc=example,dc=org?cn?sub?(uid=alice)'`; also without `-u`.
- **Expected (REF2):** `curl: (64) SSL: no alternative certificate subject name matches target ipv4 address '127.0.0.1'`, exit 64, one line.
- **Actual (.NET):** `curl: (60) SSL: no alternative certificate subject name …` plus the sslcerts help paragraph, exit 60.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapStartTls.cs`: map a verification failure during StartTLS to `CurlExitCode.UseSslFailed` (64).
- **Acceptance check:** the command exits 64 with the single-line message above.

## Acceptance criteria

- [ ] the command exits 64 with the single-line message above.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: 1eed013a 69116afe 3d836d5e.
