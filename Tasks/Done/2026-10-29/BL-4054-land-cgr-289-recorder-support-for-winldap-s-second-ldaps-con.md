---
id: BL-4054
title: Land CGR-289 recorder support for WinLDAP's second ldaps connection and the -k system-store trust fix
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4046]
touches: [Record-CurlExchange.ps1, Curl.Networking.UnitLibrary/ISystemStoreTrustTlsProvider.cs, Curl.Networking.UnitTests/ConnectTargetTlsOverrideTests.cs, Curl.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4054 — Land CGR-289 recorder support for WinLDAP's second ldaps connection and the -k system-store trust fix

## Goal

Record-CurlExchange.ps1 -Script -Tls serves WinLDAP's second ldaps connection, and a system-store-only TLS connection still honours -k.

## Context

CGR-289, part 1 of 3 of BL-4044. The recorder handshook only the first TLS connection, so WinLDAP's second one went unanswered (real curl waited 30 s, exit 38). The system-store-only provider also dropped -k (Curl exit 60 SEC_E_UNTRUSTED_ROOT); `ISystemStoreTrustTlsProvider.SystemStoreOnly` must keep `Insecure` (ADR-0983, to be written with a README row).

## Acceptance criteria

- [x] `Record-CurlExchange.ps1 -Script -Tls` handshakes every connection that speaks, reads each inside TLS, and serves the session on the first to send bytes there; a run against real curl `-sS -k ldaps://localhost:PORT/dc=test` records 2 TLS connections and no 30 s wait.
- [x] A unit test in `ConnectTargetTlsOverrideTests.cs` pins that the system-store-only provider keeps `Insecure`, and ADR-0983 records the decision.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-4044 (cost cap).
- Continues the stashed work of BL-4044.
- Added `Curl.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs` to touches: only the doc comment of `WinLdapTargetOf`, which said WinLDAP's connection ignores `-k` and became false with this fix. No task in Doing on origin/work/dark-factory names it; BL-4055 (Backlog) does and edits code there later.
- The ADR was written as ADR-0983, a number another lane already used; renumbered to ADR-0992 (ADR-0818); the criterion's ADR-0983 is that ADR.
- Measured 2026-10-29 with curl 8.22.0 (Schannel): `Record-CurlExchange.ps1 -Port 18636 -Tls -Script` (read ber, close) with `-sS -k ldaps://localhost:18636/dc=test` recorded "TLS handshake completed on connection 1" and "on connection 2", curl exit 38 after 5.4 s (the script closes after one read), no 30 s wait.
- Measure-CodeQuality.ps1 for Curl.Networking.UnitLibrary and Curl.Protocol.Ldap.UnitLibrary: 0 failing members. Fast tests green.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 42fabf5a 9c94189d 779790c0.
- 2026-10-29: Doing -> Done. Recorder serves WinLDAP's second ldaps connection (measured against curl 8.22.0); system-store-only trust keeps -k (ADR-0992).
