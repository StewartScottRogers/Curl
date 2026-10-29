---
id: BL-4056
title: Measure CGR-289 ldaps:// --cacert variant against real curl and close BL-3818
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4046, BL-4055]
touches: [Curl.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Protocol.Ldap.UnitTests/Cgr289Tests.cs, Record-CurlExchange.ps1]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4056 — Measure CGR-289 ldaps:// --cacert variant against real curl and close BL-3818

## Goal

The `--cacert` ldaps:// variant of CGR-289 matches real curl, and BL-3818 is closed.

## Context

CGR-289, part 3 of 3 of BL-4044. With a non-issuing CA (Curl.Output.UnitTests/Fixtures/LoopbackChain.pem) both already exit 60 with the same stderr. Measure the trusted-`--cacert` variant (gap report: Server Down) once BL-4054 and the -k match are in; pass `-TlsRootCertificateFile` directly to Record-CurlExchange.ps1, not splatted (it wrote no file in a hashtable-splatted run; fix the recorder if it still fails). When done, tick BL-3818's criteria from the Notes of BL-4044 and move BL-3818 to Done with the board script.

## Acceptance criteria

- [x] `curl -sS --cacert CA ldaps://localhost:PORT/dc=test` (trusted CA) gives the same exit code, stderr and wire bytes through real curl and Curl; the result is recorded in Notes, and `Cgr289Tests.cs` pins any difference fixed.
- [x] BL-3818's criteria are ticked and BL-3818 is in Done.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-4044 (cost cap).
- 2026-10-29 lane 2: touches widened to Curl.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs for the fix below; no task in Doing on origin/work/dark-factory named it.
- Measured with Record-CurlExchange.ps1 -Script -Tls -TlsRootCertificateFile (passed directly, not splatted: it wrote the root PEM, so the recorder needed no fix), curl 8.22.0 vs Curl. The throwaway certificate names 127.0.0.1, not localhost, so localhost gives both the same exit 60 CertGetNameString name mismatch, and without --ssl-no-revoke both exit 60 "revocation status is unknown". The real repro is `curl -sS --cacert ROOT --ssl-no-revoke ldaps://127.0.0.1:PORT/dc=test`.
- Real curl: first handshake verifies and sends nothing; WinLDAP's second handshake fails (root not in the system store); exit 38 `curl: (38) LDAP local: bind via ldap_win_bind Server Down`, no stdout, 0 request bytes. Curl before: exit 60 `SEC_E_UNTRUSTED_ROOT` on the second connection. Fixed: LdapProtocolHandler.WinLdapConnectFailure ends an ldaps WinLDAP connect failure as the bind's Server Down (ADR-0994). Curl after: exit 38 with identical stderr, no stdout, 0 request bytes (only the TLS layer differs: Curl's handshake completes and is closed, as the client checks trust after it).
- Pinned in Cgr289Tests: ExecuteAsync_LdapsWinLdapHandshakeFails_FailsWithServerDown, ExecuteAsync_LdapWinLdapConnectFails_FailsWithTheConnectFailure (plaintext unchanged, not measured). Ldap tests 646 pass; full fast run 0 failed; Measure-CodeQuality for Curl.Protocol.Ldap.UnitLibrary: 0 failing members.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 08162493 8cf8d85c 779790c0.
- 2026-10-29: Doing -> Done. ldaps:// with a trusted --cacert ends as curl's exit 38 bind Server Down, not exit 60
