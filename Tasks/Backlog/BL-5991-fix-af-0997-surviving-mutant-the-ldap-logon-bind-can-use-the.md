---
id: BL-5991
title: Fix AF-0997: Surviving mutant: the LDAP logon bind can use the Negotiate package for NTLM and NTLM for Negotiate (== -> !=)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs]
requirement: none
created: 2026-11-25
completed:
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

- [ ] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs line 23`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-25: Created.
