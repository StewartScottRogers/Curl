---
id: BL-4978
title: Fix the Windows SSPI NTLM tests that fail on lane hosts
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitTests/NegotiateLogonTokenSourceTests.cs, Curl.Console.UnitTests/CurlCompositionSmtpNtlmTests.cs, Curl.Console.UnitTests/CurlCompositionNtlmTests.cs, Curl.Authentication.UnitTests/SystemSecurityContextFactoryTests.cs, Curl.Authentication.UnitTests/SaslAuthenticatorSecurityContextTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4978 — Fix the Windows SSPI NTLM tests that fail on lane hosts

## Goal

The Windows SSPI NTLM unit tests pass on the lane hosts, or are corrected to what SSPI on Windows 11 now gives.

## Context

Found by BL-4909 lane 4 on 2026-11-11 (Windows 11 Pro 10.0.26300, SDK 10.0.404): the fast tests fail in `Curl.Authentication.UnitTests` (Begin_NtlmOnSspi_SendsSspisType1AndAType3, NextTokenAsync_WindowsNtlmMeasuredChallenge_GivesSspisType3, NextTokenAsync_WindowsNtlmExplicitCredential_GivesSspisType1), `Curl.Original.Protocol.Ldap.UnitTests` (Start_NtlmWithNoProtection_FirstTokenAsksNeitherToSignNorSeal) and `Curl.Console.UnitTests` (RunAsync_WindowsProductionRoute_SendsSspisType1AndAType3AsCurl8210Measured, RunAsync_WindowsProductionRoute_SendsSspisType1AndType3AsCurl8210Measured). None touches HTTP continue-wait code; they all call SSPI NTLM on the host, so a host change (an OS update, or NTLM disabled by policy) is the likely cause. They also make `Measure-CodeQuality.ps1 -Library` abort for any library Curl.Console.UnitTests reaches. Start by running one alone and reading the SSPI status it gets.

## Acceptance criteria

- [ ] The six tests named under Context pass on a lane host, or each is pinned to the answer SSPI on this Windows build gives, with the reason in an ADR.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-11: Created.
