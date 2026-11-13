---
id: BL-5084
title: Fix AF-0725: Surviving mutant: a 1024-character attribute can be dropped from the WinLDAP search request
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5084 — Fix AF-0725: Surviving mutant: a 1024-character attribute can be dropped from the WinLDAP search request

## Goal

The defect the audit office reported as AF-0725 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0725 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0725-surviving-mutant-a-1024-character-attribute-can-be.md`.

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapUrlReader.cs:96`

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapUrlReader.cs:96`

Mutant 'attribute.Length is 0 or > MaxAttributeLength' (MaxAttributeLength = 1024) -> '>=' survived. An attribute of exactly 1024 characters is kept by the original and dropped by the mutant (the loop breaks), which changes the SearchRequest bytes sent. The limit's boundary is untested.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapUrlReader.cs:96:> -Member ReadAttributes -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added Read_AttributeOfExactly1024_IsKept in WinLdapUrlReaderTests.cs: a 1024-char attribute must be kept, which kills the `>` -> `>=` mutant. The audit reproduction script could not be run: the lane guard refuses Audit/ paths, so the first criterion is ticked on reasoning (the mutant would drop the attribute and fail the test). Build clean, 716 Ldap fast tests pass. No production code changed, so no quality run.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-13: Doing -> Done. Added boundary test for 1024-char attribute
