---
id: BL-5141
title: Exchange an NTLM session key and seal messages with NTLM session security
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Ntlm.UnitLibrary/NtlmChallengeAnswerer.cs, Curl.Ntlm.UnitLibrary/NtlmAuthenticateMessage.cs, Curl.Ntlm.UnitLibrary/NtlmSessionSecurity.cs, Curl.Ntlm.UnitTests/NtlmSessionSecurityTests.cs, Curl.Ntlm.UnitTests/NtlmChallengeAnswererKeyExchangeTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5141 — Exchange an NTLM session key and seal messages with NTLM session security

## Goal

`Curl.Ntlm` can answer a challenge with a fresh exported session key (NEGOTIATE_KEY_EXCH, `EncryptedRandomSessionKey` in the AUTHENTICATE message) and seal, sign, unseal and verify messages with NTLM session security (MS-NLMP 3.4), for the LDAP SASL security layer of BL-5142.

## Context

Split from BL-5094 (finding STD-049 of the 2026-11-13 outside fitness review). The LDAP Sicily NTLM bind with `-u` must seal every later PDU as curl's WinLDAP does, and `Curl.Ntlm` has neither piece today: `NtlmChallengeAnswerer.Answer` throws away `NtlmResponses.KeyExchangeKey`, `NtlmAuthenticateMessage` has no session key field (its EncryptedRandomSessionKey buffer at offset 52 is always empty), and nothing derives the sign and seal keys. `NtlmResponseComputation.EncryptSessionKey` and `Curl.Cryptography`'s `Rc4` already exist.

Key exchange must be opt-in: curl's HTTP NTLM never sends a session key, so the HTTP path's AUTHENTICATE bytes must not change.

## Acceptance criteria

- [ ] An opt-in answer (a new method or parameter on `NtlmChallengeAnswerer`) draws a 16-byte exported session key from `INtlmRandomSource`, writes `RC4K(KeyExchangeKey, ExportedSessionKey)` as the AUTHENTICATE message's EncryptedRandomSessionKey when the challenge negotiates KEY_EXCH, and hands the exported session key to its caller; the existing `Answer` encodes the same bytes as before (existing tests green).
- [ ] `NtlmSessionSecurity` (client side and server side, from the exported session key and negotiated flags with extended session security, 128-bit) derives the MS-NLMP sign and seal keys, and seals/signs (`RC4(SealKey, message)` plus the 16-byte signature: version 1, `RC4(HMAC_MD5(SignKey, seq + message)[0..8])` under KEY_EXCH, sequence number) and unseals/verifies, with a sequence number per direction; a tampered message or signature fails verification.
- [ ] Tests pin the keys and a sealed message against the MS-NLMP 4.2.4 test vectors (NTLMv2 with extended session security).
- [ ] `Curl.Ntlm.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 (`Measure-CodeQuality.ps1 -Library Curl.Ntlm.UnitLibrary`).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Filed by BL-5094's run on lane 4, which split that task for size before any edit.

## Log

- 2026-11-13: Created.
