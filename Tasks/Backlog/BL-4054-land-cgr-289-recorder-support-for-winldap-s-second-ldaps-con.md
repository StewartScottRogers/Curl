---
id: BL-4054
title: Land CGR-289 recorder support for WinLDAP's second ldaps connection and the -k system-store trust fix
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4046]
touches: [Record-CurlExchange.ps1, Curl.Networking.UnitLibrary/ISystemStoreTrustTlsProvider.cs, Curl.Networking.UnitTests/ConnectTargetTlsOverrideTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4054 — Land CGR-289 recorder support for WinLDAP's second ldaps connection and the -k system-store trust fix

## Goal

Record-CurlExchange.ps1 -Script -Tls serves WinLDAP's second ldaps connection, and a system-store-only TLS connection still honours -k.

## Context

CGR-289, part 1 of 3 of BL-4044. The recorder handshook only the first TLS connection, so WinLDAP's second one went unanswered (real curl waited 30 s, exit 38). The system-store-only provider also dropped -k (Curl exit 60 SEC_E_UNTRUSTED_ROOT); `ISystemStoreTrustTlsProvider.SystemStoreOnly` must keep `Insecure` (ADR-0983, to be written with a README row).

## Acceptance criteria

- [ ] `Record-CurlExchange.ps1 -Script -Tls` handshakes every connection that speaks, reads each inside TLS, and serves the session on the first to send bytes there; a run against real curl `-sS -k ldaps://localhost:PORT/dc=test` records 2 TLS connections and no 30 s wait.
- [ ] A unit test in `ConnectTargetTlsOverrideTests.cs` pins that the system-store-only provider keeps `Insecure`, and ADR-0983 records the decision.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-4044 (cost cap).
- Continues the stashed work of BL-4044.

## Log

- 2026-10-29: Created.
