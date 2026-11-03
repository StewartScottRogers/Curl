---
id: BL-4766
title: Fix AF-0602: Surviving mutant: lib1560's huge-part check can make every part but one huge without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4766 — Fix AF-0602: Surviving mutant: lib1560's huge-part check can make every part but one huge without a test failing

## Goal

The defect the audit office reported as AF-0602 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0602 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0602-surviving-mutant-lib1560-s-huge-part-check-can-mak.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs:108`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs:108`

piece == i ? BigPart : "c" -> piece != i survived: the check then builds URLs with six huge parts instead of one, a different upstream check whose error count reaches lib1560's output; no test pins Huge's result.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs:108:== -Member Huge -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
