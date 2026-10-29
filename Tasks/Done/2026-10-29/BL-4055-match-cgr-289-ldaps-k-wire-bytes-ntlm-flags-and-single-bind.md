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
completed: 2026-10-29
---
# BL-4055 â€” Match CGR-289 ldaps:// -k wire bytes, NTLM flags and single bind against real curl

## Goal

`curl -sS -k ldaps://localhost:PORT/dc=test` through Curl matches curl 8.22.0 on Windows: 2 connections, 3 RootDSE searches, one NTLM bind with flags B7B208E2, one unbind, same exit code and stderr.

## Context

CGR-289, part 2 of 3 of BL-4044. Real curl measured: bind flags `07 b2 08 a2`... compare against Curl's `b7 b2 08 e2` and its exit 38 text `... Timeout` versus curl's `LDAP local: bind via ldap_win_bind Unavailable`; BL-4046 covers the flags and text, so re-measure first and fix any remaining difference in WinLdapLogonBind.cs / LdapProtocolHandler.cs. Use the recorder from BL-4054 (`Record-CurlExchange.ps1 -Script -Tls`).

## Acceptance criteria

- [x] `curl -sS -k ldaps://localhost:PORT/dc=test` gives the same exit code, stderr and wire bytes (searches, bind flags, bind count, unbind) through real curl and Curl; the result is recorded in Notes.
- [x] `Cgr289Tests.cs` pins the matched behaviour.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-4044 (cost cap).n- Measured 2026-10-29 with Record-CurlExchange.ps1 -Script -Tls, curl 8.22.0 vs Curl, -sS -k ldaps://localhost:PORT/dc=test. (a) Script answers the 3 RootDSE searches, then goes silent: both exit 38 curl: (38) LDAP local: bind via ldap_win_bind Unavailable, 2 TLS connections, request.bin identical byte for byte (3 searches, sicilyNegotiate with NTLM flags 07 b2 08 a2). (b) Script also answers with an NTLM challenge, the bind and the search: both exit 0 with empty stderr, one negotiate + one authenticate bind (flags 05 82 08 a2), one dc=test search (id 6), one unbind (id 7); only the NTLM authenticate's random/MIC bytes differ. BL-4046 had already closed the flags and text gap, so no product change was needed.n- Pinned (b) in Cgr289Tests.ExecuteAsync_LdapsSicilyBind_BindsOnceSearchesOnceAndUnbindsAsMessageSeven; (a) was already pinned. Only test code changed, so there was no Measure-CodeQuality run. dotnet build clean; fast tests: 46 projects passed, 0 failed (Ldap 644).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d35ea53d 340d318a 779790c0.
- 2026-10-29: Doing -> Done. ldaps:// -k through Curl matches curl 8.22.0 byte for byte: searches, NTLM flags, one bind, unbind, exit and stderr
