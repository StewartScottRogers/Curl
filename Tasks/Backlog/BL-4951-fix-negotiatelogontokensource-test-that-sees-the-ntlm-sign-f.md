---
id: BL-4951
title: Fix NegotiateLogonTokenSource test that sees the NTLM sign flag on this Windows host
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitTests/NegotiateLogonTokenSourceTests.cs, Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4951 — Fix NegotiateLogonTokenSource test that sees the NTLM sign flag on this Windows host

## Goal

`NegotiateLogonTokenSourceTests.Start_NtlmWithNoProtection_FirstTokenAsksNeitherToSignNorSeal` passes on every Windows host, or the code stops asking SSPI for signing when no protection is wanted.

## Context

Found by BL-4898's fast-test run on lane 4 (2026-11-11, Windows 11 Pro 10.0.26300): the test fails at `NegotiateLogonTokenSourceTests.cs:70`, `Assert.AreEqual(0, token[12] & 0x30)`: expected 0, got 16 (0x10, NTLMSSP_NEGOTIATE_SIGN). SSPI on this host sets the sign flag in the NTLM negotiate message even with no protection asked for, perhaps by a policy or OS update. Unrelated to BL-4898's plugin change. Start by checking which context requirements `NegotiateLogonTokenSource` passes to `InitializeSecurityContext`, and whether curl 8.22.0 (Schannel) sends the same flag on this host, measured with `Record-CurlExchange.ps1`.

## Acceptance criteria

- [ ] The test passes on this host, and what the NTLM negotiate token's sign and seal flags hold matches curl's on the same host, or the test pins only what Curl controls.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-11: Created.
