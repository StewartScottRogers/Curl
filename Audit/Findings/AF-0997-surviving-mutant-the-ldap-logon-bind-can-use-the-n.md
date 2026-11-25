---
id: AF-0997
title: Surviving mutant: the LDAP logon bind can use the Negotiate package for NTLM and NTLM for Negotiate (== -> !=)
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs:NegotiateLogonAuthentication-eq:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs:23:==
task: BL-5991
tasks: BL-5991
found: 2026-11-25
found-at: eb33fe3c5ab162945003079cf4f807e5608dde01
scorecard: 2026-11-25_1306.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0997 - Surviving mutant: the LDAP logon bind can use the Negotiate package for NTLM and NTLM for Negotiate (== -> !=)

## Summary

Medium finding from the quality auditor at `Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs:23`: Surviving mutant: the LDAP logon bind can use the Negotiate package for NTLM and NTLM for Negotiate (== -> !=).

## Evidence

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs:23`

Mutant 'Package = package == LdapLogonPackage.Ntlm ? "NTLM" : "Negotiate"' -> '!=' survived. Start_NtlmFirstToken_IsTheNtlmNegotiateMessageAskingToSignAndSeal cannot tell because, outside a domain, Windows' Negotiate package also answers with a raw NTLMSSP message; inside a domain the swap sends a Kerberos SPNEGO token where WinLDAP sends NTLM (a different bind request). No test checks which package name is chosen.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs:23:== -Member NegotiateLogonAuthentication -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-25: filed proposed.
