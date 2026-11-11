---
id: BL-4951
title: Fix the Windows SSPI NTLM tests that fail since SSPI's Type 1 flags changed on the lane host
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Authentication.UnitTests/SaslAuthenticatorSecurityContextTests.cs, Curl.Authentication.UnitTests/SystemSecurityContextFactoryTests.cs, Curl.Original.Protocol.Ldap.UnitTests/NegotiateLogonTokenSourceTests.cs, Curl.Console.UnitTests/CurlCompositionNtlmTests.cs, Curl.Console.UnitTests/CurlCompositionSmtpNtlmTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4951 — Fix the Windows SSPI NTLM tests that fail since SSPI's Type 1 flags changed on the lane host

## Goal

The Windows-only SSPI NTLM tests pass again on a Windows host whose SSPI now sends different NTLM Type 1 flags.

## Context

Found by BL-4902's fast-test run on lane 1, 2026-11-11 (built with SDK 11.0.100 because 10.0.401 is missing, BL-4952). Six tests fail, all comparing a token Windows SSPI made against bytes measured earlier:
- `Curl.Authentication.UnitTests`: `SaslAuthenticatorSecurityContextTests.Begin_NtlmOnSspi_SendsSspisType1AndAType3` (line 334: element 12 expected 7, actual 151), `NextTokenAsync_WindowsNtlmMeasuredChallenge_GivesSspisType3`, `NextTokenAsync_WindowsNtlmExplicitCredential_GivesSspisType1`.
- `Curl.Original.Protocol.Ldap.UnitTests`: `Start_NtlmWithNoProtection_FirstTokenAsksNeitherToSignNorSeal`.
- `Curl.Console.UnitTests`: `RunAsync_WindowsProductionRoute_SendsSspisType1AndType3AsCurl8210Measured`, `RunAsync_WindowsProductionRoute_SendsSspisType1AndAType3AsCurl8210Measured`.
Byte 12 of a Type 1 message is the low byte of its flags (7 -> 151 adds NTLMSSP_NEGOTIATE_SIGN and 0x80). Likely a Windows update changed SSPI's defaults. Measure what real curl (Schannel/SSPI build) sends now, and decide whether the tests should pin SSPI's exact bytes or only the fields Curl controls.

## Acceptance criteria

- [ ] The six tests named above pass on the lane host, or are rewritten to check only what Curl controls, with the reason under Notes.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-11: Created.
