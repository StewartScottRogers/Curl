---
id: AF-0378
title: Constructor_WithNoArguments_LeavesTheTenOptionsOfAdr0151NotGiven checks eight defaults, not ten
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-24_0845.md: the reproduction no longer reproduces.
key: quality:Curl.Networking.UnitTests/TlsClientOptionsTests.cs:Constructor_WithNoArguments_LeavesTheTenOptionsOfAdr0151NotGiven:name-lies
reproduction: none
task: BL-3355
tasks: BL-3355
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed: 2026-10-24
closed-how: reliable-reaudit
closed-by: 2026-10-24_0845.md
---
# AF-0378 - Constructor_WithNoArguments_LeavesTheTenOptionsOfAdr0151NotGiven checks eight defaults, not ten

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitTests/TlsClientOptionsTests.cs:75`: Constructor_WithNoArguments_LeavesTheTenOptionsOfAdr0151NotGiven checks eight defaults, not ten.

## Evidence

Location: `Curl.Networking.UnitTests/TlsClientOptionsTests.cs:75`

The body asserts eight properties: Curves, SignatureAlgorithms, AllowEarlyData, Ech, EchPublicName, EchConfigList, SslSessionsFile and Engine. The name promises ten. Phase 2: explained in part by ADR-0151, whose ten options include --dump-ca-embed and others with no TlsClientOptions field, so ten cannot all be checked here. The name still claims more than the body checks.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TlsClientOptionsTests.cs -Pattern 'LeavesTheTenOptionsOfAdr0151NotGiven' -Context 0,20
```

- Expected: Ten options asserted, or a name that says what is checked.
- Actual: Eight Assert lines (lines 86 to 93).

## Re-audits

- 2026-10-23 | 2026-10-23_2322.md | reproduces: no | Ran the Select-String reproduction: no match for 'LeavesTheTenOptionsOfAdr0151NotGiven'. The test is now Constructor_WithNoArguments_LeavesTheEightTlsClientOptionsFieldsOfAdr0151NotGiven (TlsClientOptionsTests.cs:75) and asserts those eight fields, so name and body agree.
- 2026-10-24 | 2026-10-24_0845.md | reproduces: no | The reproduction's pattern 'LeavesTheTenOptionsOfAdr0151NotGiven' no longer matches. The test is now Constructor_WithNoArguments_LeavesTheEightTlsClientOptionsFieldsOfAdr0151NotGiven and asserts all eight fields it names (Curves, SignatureAlgorithms, AllowEarlyData, Ech, EchPublicName, EchConfigList, SslSessionsFile, Engine).

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> accepted.
- 2026-10-24: accepted -> closed. Re-audit 2026-10-24_0845.md: the reproduction no longer reproduces.
