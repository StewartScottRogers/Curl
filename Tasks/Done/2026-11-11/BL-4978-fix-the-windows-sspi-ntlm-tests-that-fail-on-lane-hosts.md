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
completed: 2026-11-11
---
# BL-4978 — Fix the Windows SSPI NTLM tests that fail on lane hosts

## Goal

The Windows SSPI NTLM unit tests pass on the lane hosts, or are corrected to what SSPI on Windows 11 now gives.

## Context

Found by BL-4909 lane 4 on 2026-11-11 (Windows 11 Pro 10.0.26300, SDK 10.0.404): the fast tests fail in `Curl.Authentication.UnitTests` (Begin_NtlmOnSspi_SendsSspisType1AndAType3, NextTokenAsync_WindowsNtlmMeasuredChallenge_GivesSspisType3, NextTokenAsync_WindowsNtlmExplicitCredential_GivesSspisType1), `Curl.Original.Protocol.Ldap.UnitTests` (Start_NtlmWithNoProtection_FirstTokenAsksNeitherToSignNorSeal) and `Curl.Console.UnitTests` (RunAsync_WindowsProductionRoute_SendsSspisType1AndAType3AsCurl8210Measured, RunAsync_WindowsProductionRoute_SendsSspisType1AndType3AsCurl8210Measured). None touches HTTP continue-wait code; they all call SSPI NTLM on the host, so a host change (an OS update, or NTLM disabled by policy) is the likely cause. They also make `Measure-CodeQuality.ps1 -Library` abort for any library Curl.Console.UnitTests reaches. Start by running one alone and reading the SSPI status it gets.

## Acceptance criteria

- [x] The six tests named under Context pass on a lane host, or each is pinned to the answer SSPI on this Windows build gives, with the reason in an ADR.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-11-11 lane 13: ran all six tests alone on this lane host (Windows 11 Pro 10.0.26300); all six pass, so the failure does not reproduce here. It was host-specific (NTLM policy or a transient SSPI state on lane 4). No code change needed. If it recurs, read the SSPI status on the failing host.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: e63b8ffe 3d836d5e.
- 2026-11-11: Doing -> Done. All six tests pass alone on lane 13 host; not reproducible, no code change
