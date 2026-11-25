---
id: BL-5908
title: Match OpenLDAP on ldap:// StartTLS with --ssl-reqd, --ssl or an untrusted certificate on Linux
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapStartTls.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapStartTls]
requirement: none
created: 2026-11-24
completed:
---
# BL-5908 — Match OpenLDAP on ldap:// StartTLS with --ssl-reqd, --ssl or an untrusted certificate on Linux

## Goal

On Linux (OpenLDAP-backed curl) the port answers `ldap://` StartTLS with `--ssl-reqd`, `--ssl` or an untrusted certificate with the exit code and request bytes that upstream curl 8.21.0 linked against OpenLDAP gives.

## Context

From Surl's Phase 7 handoff `Z:\repos\Surl\Documentation\Conformance\CurlPort\Port-Board-Handoff.md`, entry 27 (Surl `Ldap.md` finding L-4). Seen on linux-x64 in Surl's `curl-port-phase-7` runs 44044472038 and 45412760425 at port commit `74f3f45` (2026-11-16). Check each case still reproduces on the current branch first.

Three cases (Surl's `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests`):

| Case | Upstream (OpenLDAP) | Port |
| --- | --- | --- |
| `--ssl-reqd`, server has no certificate | exit 1, `Unsupported protocol` | exit 64 |
| `--ssl`, server has no certificate | carries on in clear with a version 2 bind (`0x02` at offset 40), exit 0 | version 3 bind (`0x03`) |
| untrusted certificate | exit 64, `SSL certificate OpenSSL verify result: self-signed certificate (18)` | exit 60, same verify result |

What decides: curl 8.21.0 manual (`--ssl`, `--ssl-reqd`, exit codes 1, 60 and 64), RFC 4513 section 3, and upstream's `lib/openldap.c` (it falls back to an LDAPv2 bind after a failed optional StartTLS). Measure upstream's OpenLDAP build with `Record-CurlExchange.ps1` before pinning. Start at `LdapStartTls.cs`. The Windows (WinLDAP) behaviour must not change.

## Acceptance criteria

- [ ] Each of the three cases gives upstream's OpenLDAP exit code, message and bind version on the port's OpenLDAP path, pinned by a unit test each against a test-double LDAP server.
- [ ] The WinLDAP path's existing StartTLS tests still pass unchanged.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from Surl Port-Board-Handoff.md entry 27.
- 2026-11-24: Backlog -> Doing. Criteria at claim: a9e74800 545cbb75 3d836d5e.
