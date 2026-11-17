---
id: BL-5314
title: Bind LDAP -u --negotiate with SASL GSS-SPNEGO after a RootDSE read, not Sicily NTLM
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapAuthenticatedBind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapLogonBind.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapAuthenticatedBindNegotiateTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5314 — Bind LDAP -u --negotiate with SASL GSS-SPNEGO after a RootDSE read, not Sicily NTLM

## Goal

`curl "ldap://host/dc=x" -u user:pw --negotiate` reads the RootDSE and binds with SASL `GSS-SPNEGO` (or SASL `DIGEST-MD5` when the server does not offer it), as WinLDAP's `LDAP_AUTH_NEGOTIATE` does, and no longer sends a Sicily NTLM bind; `--ntlm` still sends the Sicily bind.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-075 (Medium, Confirmed: 3 runs). Area: LDAP bind with explicit credentials, Windows build. A silent wrong authentication mechanism.

**Repro.** An LDAP BER server on 127.0.0.1:34025 (PowerShell or a C# file-based app, never Python) that answers RootDSE searches (base `""`) with `supportedLDAPVersion: 3` and `supportedSASLMechanisms: GSS-SPNEGO, DIGEST-MD5`; answers a Sicily negotiate bind (tag 0x8a) with resultCode 0 and an NTLM Type-2 in matchedDN; answers a 0x8b bind with resultCode 0; logs every PDU in hex.
1. `curl -sS -m 15 "ldap://127.0.0.1:34025/dc=x" -u admin:pw --negotiate`
2. Repeat with the server advertising only `DIGEST-MD5`.

**Expected (reference curl, WinLDAP `LDAP_AUTH_NEGOTIATE`).** The reference reads the RootDSE (`supportedCapabilities`, `supportedSASLMechanisms`), then sends a SASL bind (tag 0xa3) with mechanism `GSS-SPNEGO` (SPNEGO/Kerberos with a reachable domain, raw NTLMSSP otherwise). Without GSS-SPNEGO it uses SASL `DIGEST-MD5`. It never sends a Sicily 0x8a/0x8b bind for `--negotiate`.

**Actual (port).** No RootDSE read; a Sicily `BindRequest name="NTLM"` with tag 0x8a and NTLMSSP Type-1, then 0x8b Type-3, whatever the server advertises. In a domain with NTLM disabled `--negotiate` would fail where the reference uses Kerberos (inferred; no domain controller was available).

**Where to start.** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapAuthenticatedBind.cs`, `BindAsync` (line 41): every kind except Digest goes to `NtlmAsync` (line 55), including `LdapBindKind.Negotiate`. The logged-on-user path already does the RootDSE read and GSS-SPNEGO bind (`WinLdapLogonBind.cs`, `LdapLogonPackage.Negotiate`); reuse it with `-u` credentials. `LdapDigestMd5Bind.cs` holds the DIGEST-MD5 bind. Because the port has no Kerberos/SPNEGO from a `NetworkCredential` in the BCL on every platform, check what `NegotiateLogonTokenSource.cs` can do with explicit credentials, and state in the Log how the SPNEGO token is produced.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Ldap.UnitTests/LdapAuthenticatedBindNegotiateTests.cs` (scripted `LdapExchange` stand-in, no network) prove that with `LdapBindKind.Negotiate` and `-u` credentials the PDUs go out in this order: RootDSE search first, then a SASL bind (tag 0xa3) with mechanism `GSS-SPNEGO`, and no Sicily 0x8a bind.
- [ ] A test proves that when the RootDSE advertises only `DIGEST-MD5` the bind is SASL `DIGEST-MD5`.
- [ ] A test proves `LdapBindKind.Ntlm` (`--ntlm`) still sends the Sicily 0x8a bind with SIGN|SEAL, unchanged.
- [ ] The Log records the review's hand check against the BER stand-in: PORT's RootDSE searches come first, then a 0xa3 bind with `GSS-SPNEGO`, and no 0x8a bind; with the DIGEST-MD5-only server the bind is `DIGEST-MD5`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell or a C# file-based app, never Python (CLAUDE.md). Hand-build any SPNEGO piece the BCL lacks in its own UnitLibrary rather than adding a package.
- Tests must pass on Windows, Linux and macOS.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 6710ff9a d9690f6a 10d46feb 3b1330a0 a0b49a29.
