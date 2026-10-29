---
id: BL-4039
title: Finish CGR-289: measure ldaps:// WinLDAP against real curl and match the NTLM flags and single bind
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Protocol.Ldap.UnitLibrary/WinLdapLogonBind.cs, Curl.Protocol.Ldap.UnitTests/Cgr289Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4039 — Finish CGR-289: measure ldaps:// WinLDAP against real curl and match the NTLM flags and single bind

## Goal

Windows ldaps:// through Curl matches curl 8.22.0 (Schannel, WinLDAP) for CGR-289: the second connection (machine-name SNI, system store only), 3 RootDSE searches, one NTLM bind with flags B7B208E2, one unbind.

## Context

BL-3818 made LdapProtocolHandler open WinLDAP's second ldaps connection with TlsServerName = Environment.MachineName and TrustsSystemStoreOnly (ADR-0979), pinned in Cgr289Tests.cs. It is not yet measured against real curl. Start with a TLS server that answers the 3 RootDSE searches and the NTLM bind as the gap report's tsrv does (Record-CurlExchange.ps1 -Script -Tls); a first attempt with a generic script made real curl wait 30 s and exit 38, and Curl hang. Then compare wire bytes, NTLM flags and the bind count, and fix any difference in WinLdapLogonBind.cs. When done, also tick BL-3818's criteria from your Notes and move BL-3818 to Done.

## Acceptance criteria

- [ ] `curl -sS -k ldaps://localhost:PORT/dc=test` and the `--cacert` variant give the same exit code, stderr and wire bytes through real curl and Curl; both results are recorded in Notes.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
