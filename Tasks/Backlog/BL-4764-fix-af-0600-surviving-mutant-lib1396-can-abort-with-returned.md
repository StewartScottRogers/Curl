---
id: BL-4764
title: Fix AF-0600: Surviving mutant: lib1396 can abort with 'returned NULL!' on an empty input without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4764 — Fix AF-0600: Surviving mutant: lib1396 can abort with 'returned NULL!' on an empty input without a test failing

## Goal

The defect the audit office reported as AF-0600 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0600 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0600-surviving-mutant-lib1396-can-abort-with-returned-n.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1396.cs:134`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1396.cs:134`

taken < 0 -> taken <= 0 survived: a row whose escaped length is 0 then gives null and the program aborts, changing the lib1396 output the upstream case compares.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1396.cs:134:< -Member Escape -ExcludeBaselineFailures -TimeoutSeconds 600
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
