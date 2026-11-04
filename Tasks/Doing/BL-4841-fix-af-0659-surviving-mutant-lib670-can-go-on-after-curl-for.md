---
id: BL-4841
title: Fix AF-0659: Surviving mutant: lib670 can go on after curl_formadd refuses its field
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4841 — Fix AF-0659: Surviving mutant: lib670 can go on after curl_formadd refuses its field

## Goal

The defect the audit office reported as AF-0659 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0659 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0659-surviving-mutant-lib670-can-go-on-after-curl-forma.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:129`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:129`

Mutant 'return false;' -> 'return true;' after a refused curl_formadd survived. The port then performs with a broken form instead of stopping, which changes its output. No test fails.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:129:false -Member BindForm -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
