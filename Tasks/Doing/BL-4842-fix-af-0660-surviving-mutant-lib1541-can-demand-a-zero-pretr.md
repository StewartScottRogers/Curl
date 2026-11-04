---
id: BL-4842
title: Fix AF-0660: Surviving mutant: lib1541 can demand a zero PRETRANSFER_TIME_T at the first header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4842 — Fix AF-0660: Surviving mutant: lib1541 can demand a zero PRETRANSFER_TIME_T at the first header

## Goal

The defect the audit office reported as AF-0660 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0660 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0660-surviving-mutant-lib1541-can-demand-a-zero-pretran.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:91`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:91`

Mutant 'mustBeZero: false' -> 'mustBeZero: true' for CURLINFO_PRETRANSFER_TIME_T survived. The port would then print a failure line whenever the time is nonzero. No test checks that line.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:91:false -Member CheckFirstHeader -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Lib1541CheckFirstHeader_WithEveryTimeAboveZero_WritesOkForAllButTheSpeed`, which pins every line `CheckFirstHeader` writes with every time above 0; a lane cannot run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant (line 91 `mustBeZero: true`) was applied by hand: the new test failed, and passed again once reverted. Test-only change, so no quality run.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
