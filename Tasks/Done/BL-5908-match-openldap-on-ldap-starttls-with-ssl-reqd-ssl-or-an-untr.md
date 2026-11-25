---
id: BL-5908
title: Match OpenLDAP on ldap:// StartTLS with --ssl-reqd, --ssl or an untrusted certificate on Linux
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapStartTls.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapBind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapStartTls, Curl.Original.Protocol.Ldap.UnitTests/Cgr013Tests.cs, Documentation/Planning/Decisions/ADR-0892-ldap-starts-tls-or-refuses-under-ssl.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
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

- [x] Each of the three cases gives upstream's OpenLDAP exit code, message and bind version on the port's OpenLDAP path, pinned by a unit test each against a test-double LDAP server.
- [x] The WinLDAP path's existing StartTLS tests still pass unchanged.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduction on this branch: `--ssl-reqd` without a certificate (exit 1) and the untrusted certificate (exit 64) were already fixed after Surl's port commit (ADR-0892's correction, STD-172). Only the `--ssl` LDAPv2 bind still differed. All three are pinned in `LdapStartTlsOpenLdapMeasuredTests`.
- Measured with `/mnt/c/UpstreamCurl/openldap-curl-8.21.0-linux-x86_64/curl` (Surl's pinned 8.21.0 + OpenLDAP 2.6.15 build) under WSL against a `socat` script sending scripted replies; `Record-CurlExchange.ps1` runs the Windows build and WSL has no `pwsh` on its PATH, so it was not used.
- The same measurements showed other StartTLS differences, fixed here and recorded in ADR-1363: refused `--ssl-reqd` codes (52 etc. exit 64, 49 exit 67, 50 exit 9), a server closing instead of answering (exit 7), any handshake failure (exit 64), and the simple bind's LDAPv2 retry and result-code mapping.
- Left to BL-5931: the unbind after a failed handshake, no bind after StartTLS without `-u`, and a non-ExtendedResponse reply.
- Touches widened to `LdapBind.cs`, `LdapProtocolHandler.cs` (the bind version has to reach the bind), `Cgr013Tests.cs` (three BL-3542 tests pinned answers 8.21.0 does not give; removed, replaced by the new class) and ADR-0892 (its "Amended by" line). No other task in Doing on `origin/work/dark-factory` named them. The WinLDAP tests in `Cgr013Tests` are unchanged.
- `--ai-help`: no option added or changed. Platform: scripted connections only, no OS-dependent output.

## Log

- 2026-11-24: Created from Surl Port-Board-Handoff.md entry 27.
- 2026-11-24: Backlog -> Doing. Criteria at claim: a9e74800 545cbb75 3d836d5e.
- 2026-11-24: Doing -> Done. ldap StartTLS and OpenLDAP bind failures match curl 8.21.0 (ADR-1363)
