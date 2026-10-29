---
id: BL-4055
title: Match CGR-289 ldaps:// -k wire bytes, NTLM flags and single bind against real curl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4046, BL-4054]
touches: [Curl.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Protocol.Ldap.UnitLibrary/WinLdapLogonBind.cs, Curl.Protocol.Ldap.UnitTests/Cgr289Tests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4055 — Match CGR-289 ldaps:// -k wire bytes, NTLM flags and single bind against real curl

## Goal

`curl -sS -k ldaps://localhost:PORT/dc=test` through Curl matches curl 8.22.0 on Windows: 2 connections, 3 RootDSE searches, one NTLM bind with flags B7B208E2, one unbind, same exit code and stderr.

## Context

CGR-289, part 2 of 3 of BL-4044. Real curl measured: bind flags `07 b2 08 a2`... compare against Curl's `b7 b2 08 e2` and its exit 38 text `... Timeout` versus curl's `LDAP local: bind via ldap_win_bind Unavailable`; BL-4046 covers the flags and text, so re-measure first and fix any remaining difference in WinLdapLogonBind.cs / LdapProtocolHandler.cs. Use the recorder from BL-4054 (`Record-CurlExchange.ps1 -Script -Tls`).

## Acceptance criteria

- [ ] `curl -sS -k ldaps://localhost:PORT/dc=test` gives the same exit code, stderr and wire bytes (searches, bind flags, bind count, unbind) through real curl and Curl; the result is recorded in Notes.
- [ ] `Cgr289Tests.cs` pins the matched behaviour.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-4044 (cost cap).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d35ea53d 340d318a 779790c0.
