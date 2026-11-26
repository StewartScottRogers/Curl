---
id: BL-5991
title: Fix AF-0997: Surviving mutant: the LDAP logon bind can use the Negotiate package for NTLM and NTLM for Negotiate (== -> !=)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs, Curl.Original.Protocol.Ldap.UnitTests/NegotiateLogonTokenSourceOptionsTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5991 — Fix AF-0997: Surviving mutant: the LDAP logon bind can use the Negotiate package for NTLM and NTLM for Negotiate (== -> !=)

## Goal

The defect the audit office reported as AF-0997 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0997 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs:23`

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs:23`

Mutant 'Package = package == LdapLogonPackage.Ntlm ? "NTLM" : "Negotiate"' -> '!=' survived. Start_NtlmFirstToken_IsTheNtlmNegotiateMessageAskingToSignAndSeal cannot tell because, outside a domain, Windows' Negotiate package also answers with a raw NTLMSSP message; inside a domain the swap sends a Kerberos SPNEGO token where WinLDAP sends NTLM (a different bind request). No test checks which package name is chosen.

Reproduction: the audit office's mutation test changed `Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs line 23` as the evidence above describes (the mutant operator is `==`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs line 23`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killing test: `NegotiateLogonTokenSourceOptionsTests.ClientOptions_LogonPackage_NamesThatSecurityPackage`
  (both rows, `Ntlm` and `Negotiate`).
- The package name was only reachable through a live security package, whose answer depends on
  whether the machine is in a domain. The options are now built by an internal
  `ClientOptions` method that `Start` calls, so the test reads the chosen name directly and runs
  on every platform. The expression the mutant changes moved from line 23 to line 31. No
  behaviour change, so no ADR.
- Added the new test file to `touches`; no other task in Doing on `origin/work/dark-factory`
  names it.
- Checked 2026-11-25: with `==` changed to `!=` the LDAP unit tests gave 2 failed, 951 passed;
  reverted, `dotnet build` had 0 warnings and 0 errors and all fast tests passed (LDAP 953 of 953).
  `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Ldap.UnitLibrary`: 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: be9e42c2 9742157d.
- 2026-11-25: Doing -> Done. A test now fails if the LDAP logon bind names the wrong security package (AF-0997's mutant).
