---
id: BL-3447
title: Fix AF-0400: With_TheTenOptionsOfAdr0151_CopiesTheOptionsWithTheNewValues sets and checks eight options, not ten
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-24
completed:
---
# BL-3447 — Fix AF-0400: With_TheTenOptionsOfAdr0151_CopiesTheOptionsWithTheNewValues sets and checks eight options, not ten

## Goal

The defect the audit office reported as AF-0400 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0400 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0400-with-thetenoptionsofadr0151-copiestheoptionswithth.md`.

Location: `Curl.Networking.UnitTests/TlsClientOptionsTests.cs:97`

Location: `Curl.Networking.UnitTests/TlsClientOptionsTests.cs:97`

The with-expression at lines 101-111 sets Curves, SignatureAlgorithms, AllowEarlyData, Ech, EchPublicName, EchConfigList, SslSessionsFile and Engine, and lines 118-125 assert exactly those eight. The name promises the ten options. Its sister test was renamed to Constructor_WithNoArguments_LeavesTheEightTlsClientOptionsFieldsOfAdr0151NotGiven (line 75); this one still says ten. Phase 2: explained by ADR-0151, whose ten options include --dump-ca-embed, --tlsuser, --tlspassword and --tlsauthtype, which are not TlsClientOptions fields - so the body is right and the name is not.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TlsClientOptionsTests.cs -Pattern 'With_TheTenOptionsOfAdr0151' -Context 0,30
```

- Expected: The test's name and its body agree on how many options it checks.
- Actual: TlsClientOptionsTests.cs:97: public void With_TheTenOptionsOfAdr0151_CopiesTheOptionsWithTheNewValues() - followed by eight assignments and eight assertions.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
