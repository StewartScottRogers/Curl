---
id: BL-3355
title: Fix AF-0378: Constructor_WithNoArguments_LeavesTheTenOptionsOfAdr0151NotGiven checks eight defaults, not ten
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3355 — Fix AF-0378: Constructor_WithNoArguments_LeavesTheTenOptionsOfAdr0151NotGiven checks eight defaults, not ten

## Goal

The defect the audit office reported as AF-0378 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0378 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0378-constructor-withnoarguments-leavesthetenoptionsofa.md`.

Location: `Curl.Networking.UnitTests/TlsClientOptionsTests.cs:75`

Location: `Curl.Networking.UnitTests/TlsClientOptionsTests.cs:75`

The body asserts eight properties: Curves, SignatureAlgorithms, AllowEarlyData, Ech, EchPublicName, EchConfigList, SslSessionsFile and Engine. The name promises ten. Phase 2: explained in part by ADR-0151, whose ten options include --dump-ca-embed and others with no TlsClientOptions field, so ten cannot all be checked here. The name still claims more than the body checks.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TlsClientOptionsTests.cs -Pattern 'LeavesTheTenOptionsOfAdr0151NotGiven' -Context 0,20
```

- Expected: Ten options asserted, or a name that says what is checked.
- Actual: Eight Assert lines (lines 86 to 93).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
