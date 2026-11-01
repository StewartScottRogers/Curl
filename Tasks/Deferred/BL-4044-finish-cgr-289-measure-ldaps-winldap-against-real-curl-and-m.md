---
id: BL-4044
title: Finish CGR-289: measure ldaps:// WinLDAP against real curl and match the NTLM flags and single bind
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4046]
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapLogonBind.cs, Curl.Original.Protocol.Ldap.UnitTests/Cgr289Tests.cs, Record-CurlExchange.ps1, Curl.Networking.UnitLibrary/ISystemStoreTrustTlsProvider.cs, Curl.Networking.UnitTests/ConnectTargetTlsOverrideTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4044 — Finish CGR-289: measure ldaps:// WinLDAP against real curl and match the NTLM flags and single bind

## Goal

Windows ldaps:// through Curl matches curl 8.22.0 (Schannel, WinLDAP) for CGR-289: the second connection (machine-name SNI, system store only), 3 RootDSE searches, one NTLM bind with flags B7B208E2, one unbind.

## Context

BL-3818 made LdapProtocolHandler open WinLDAP's second ldaps connection with TlsServerName = Environment.MachineName and TrustsSystemStoreOnly (ADR-0979), pinned in Cgr289Tests.cs. It is not yet measured against real curl. Start with a TLS server that answers the 3 RootDSE searches and the NTLM bind as the gap report's tsrv does (Record-CurlExchange.ps1 -Script -Tls); a first attempt with a generic script made real curl wait 30 s and exit 38, and Curl hang. Then compare wire bytes, NTLM flags and the bind count, and fix any difference in WinLdapLogonBind.cs. When done, also tick BL-3818's criteria from your Notes and move BL-3818 to Done.

## Acceptance criteria

- [ ] `curl -sS -k ldaps://localhost:PORT/dc=test` and the `--cacert` variant give the same exit code, stderr and wire bytes through real curl and Curl; both results are recorded in Notes.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-10-28 lane 9: touches widened to Record-CurlExchange.ps1 and Curl.Networking's ISystemStoreTrustTlsProvider.cs and ConnectTargetTlsOverrideTests.cs; no task in Doing on origin/work/dark-factory named them.
- Recorder: `-Script -Tls` handshook only the first connection, so WinLDAP's second TLS connection went unanswered (the earlier 30 s wait). It now handshakes every connection that speaks, reads each inside TLS, and serves the session on the first to send bytes there (left uncommitted with the rest, per the requeue rule).
- Measured real curl 8.22.0 (`-sS -k ldaps://localhost:21909/dc=test`, script answering 3 rootDSE searches with SearchResultDone IDs 1-3, then silent): 2 TLS connections, 3 searches (supportedCapabilities, supportedSASLMechanisms, supportedCapabilities) on the second, sicilyNegotiate bind with NTLM flags `07 b2 08 a2`, exit 38 `curl: (38) LDAP local: bind via ldap_win_bind Unavailable`.
- Curl before this run: exit 60 `SEC_E_UNTRUSTED_ROOT` - the system-store-only provider also dropped `-k`. Fixed: `ISystemStoreTrustTlsProvider.SystemStoreOnly` keeps `Insecure` (ADR-0983). Curl after: same 2 connections and the same 3 search messages byte for byte, then the bind with flags `b7 b2 08 e2` and exit 38 `... Timeout`. Filed BL-4046 for the flags and the text.
- `--cacert` with a CA that does not issue the server certificate (Curl.Output.UnitTests/Fixtures/LoopbackChain.pem): both exit 60 with the same stderr on the first connection. The trusted-`--cacert` variant (gap report: Server Down) still needs measuring once BL-4046 is in; Record-CurlExchange's -TlsRootCertificateFile wrote no file in my hashtable-splatted run, so pass it directly next time.
- Networking and Ldap fast tests pass (3814 and 638); Measure-CodeQuality for Curl.Networking.UnitLibrary and Curl.Original.Protocol.Ldap.UnitLibrary: 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 8d4a1a39 779790c0.
- 2026-10-28: Doing -> Backlog. Needs BL-4046 (WinLDAP NTLM flags over ldaps://); the -k trust fix, ADR-0983 and the recorder's second-TLS-connection support are left for the stash
- 2026-10-29: Backlog -> Deferred. Split into BL-4054, BL-4055, BL-4056 (cost cap)
