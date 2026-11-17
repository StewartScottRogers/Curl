---
id: BL-5376
title: Negotiate qop=auth-conf in the LDAP SASL DIGEST-MD5 bind
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5Bind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapAuthenticatedBind.cs, Curl.Original.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5376 — Negotiate qop=auth-conf in the LDAP SASL DIGEST-MD5 bind

## Goal

`curl -u user:pw --digest ldap://...` sends a SASL DIGEST-MD5 response with `qop=auth-conf` and a cipher and wraps every later PDU, as curl does (High: today the LDAP session has no confidentiality or integrity where curl protects it).

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-093 (High, Confirmed: 2 runs by the reviewer, re-reproduced by the orchestrator; the source hard-codes the value). Area: LDAP SASL DIGEST-MD5 bind, Windows. Same class as the fixed STD-049 NTLM sign/seal finding.

**Repro.**
1. An OpenLDAP server (the review used `osixia/openldap:1.5.0` in docker on 127.0.0.1:47389) whose RootDSE advertises DIGEST-MD5 and whose challenge offers `qop="auth,auth-int,auth-conf"`.
2. A TCP relay on 127.0.0.1:47880 forwarding to 47389 that logs client-to-server bytes to a file (PowerShell or a C# file-based app).
3. `curl -sS -m 10 -u admin:admin --digest "ldap://127.0.0.1:47880/ou=people,dc=example,dc=org?cn?sub"`
4. `grep -aoE 'qop=[a-z-]+(,cipher=[a-z0-9]+)?' <relay log>`

**Expected (REF).** Two RootDSE searches, then a SASL DIGEST-MD5 bind whose response contains `qop=auth-conf,cipher=3des` (step 4 prints `qop=auth-conf,cipher=3des`); after a successful bind every PDU is SASL-wrapped. Against this container both end with exit 38 `LDAP local: bind via ldap_win_bind Invalid Credentials` (no SASL users exist).
**Actual (PORT).** Step 4 prints `qop=auth`. No security layer is negotiated, so every later SearchRequest and result travels as plain BER. Same exit 38. From the source, a server that offers only `qop="auth-conf"` makes the port give up (`AnswerRequest` returns null) (Probable, not run).
**Where to start.** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5Bind.cs` line ~56 (`qop=auth` hard-coded; line ~47 requires `auth` to be offered). `LdapAuthenticatedBind.cs` routes Digest there. Implement auth-conf wrapping (3DES per RFC 2831, by hand, base class library only) or use the path the STD-049 fix used.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Ldap.UnitTests` (a new file) prove that, given a challenge offering `auth,auth-int,auth-conf`, the digest response contains `qop=auth-conf,cipher=3des` and the response value and session keys RFC 2831 gives.
- [ ] A test proves that after a successful bind every later request PDU is sent SASL-wrapped (a 4-byte length prefix, not `0x30`) and every response is unwrapped.
- [ ] A test proves a server offering only `qop="auth-conf"` still binds.
- [ ] A test proves a server offering only `qop="auth"` keeps today's behaviour.
- [ ] Tests are platform-neutral.
- [ ] Hand check recorded in the Log: step 4 prints `qop=auth-conf` with a cipher, and after a successful bind later PDUs start with a 4-byte SASL length prefix, not `0x30`. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: step 4 prints `qop=auth-conf` with a cipher; after a successful bind, later PDUs start with a 4-byte SASL length prefix, not `0x30`. A server offering only `qop="auth-conf"` still binds.
- 3DES and the digest are hand-built; no package (CLAUDE.md).

## Log

- 2026-11-17: Created.
