---
id: AF-0400
title: With_TheTenOptionsOfAdr0151_CopiesTheOptionsWithTheNewValues sets and checks eight options, not ten
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitTests/TlsClientOptionsTests.cs:With_TheTenOptionsOfAdr0151_CopiesTheOptionsWithTheNewValues:name-lies
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0400 - With_TheTenOptionsOfAdr0151_CopiesTheOptionsWithTheNewValues sets and checks eight options, not ten

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitTests/TlsClientOptionsTests.cs:97`: With_TheTenOptionsOfAdr0151_CopiesTheOptionsWithTheNewValues sets and checks eight options, not ten. Reported by an auditor flagged unreliable in 2026-10-23_2322.md.

## Evidence

Location: `Curl.Networking.UnitTests/TlsClientOptionsTests.cs:97`

The with-expression at lines 101-111 sets Curves, SignatureAlgorithms, AllowEarlyData, Ech, EchPublicName, EchConfigList, SslSessionsFile and Engine, and lines 118-125 assert exactly those eight. The name promises the ten options. Its sister test was renamed to Constructor_WithNoArguments_LeavesTheEightTlsClientOptionsFieldsOfAdr0151NotGiven (line 75); this one still says ten. Phase 2: explained by ADR-0151, whose ten options include --dump-ca-embed, --tlsuser, --tlspassword and --tlsauthtype, which are not TlsClientOptions fields - so the body is right and the name is not.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TlsClientOptionsTests.cs -Pattern 'With_TheTenOptionsOfAdr0151' -Context 0,30
```

- Expected: The test's name and its body agree on how many options it checks.
- Actual: TlsClientOptionsTests.cs:97: public void With_TheTenOptionsOfAdr0151_CopiesTheOptionsWithTheNewValues() - followed by eight assignments and eight assertions.

## Re-audits

## Log

- 2026-10-23: filed proposed.
