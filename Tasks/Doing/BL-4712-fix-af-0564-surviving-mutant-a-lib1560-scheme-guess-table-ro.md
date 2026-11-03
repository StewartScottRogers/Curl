---
id: BL-4712
title: Fix AF-0564: Surviving mutant: a lib1560 scheme-guess table row can change without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4712 — Fix AF-0564: Surviving mutant: a lib1560 scheme-guess table row can change without a test failing

## Goal

The defect the audit office reported as AF-0564 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0564 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0564-surviving-mutant-a-lib1560-scheme-guess-table-row.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.Handles.cs:22`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.Handles.cs:22`

Mutant new("smtp.example.com:99/moo", false, ...) -> new("smtp.example.com:99/moo", true, ...) survived. The row is upstream lib1560's case and Lib1560.RunAsync checks every row (checks.SchemeGuess(Lib1560Tables.SchemeGuesses)), but no unit test pins the rows, so a wrong row changes what the port checks and what it prints unnoticed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.Handles.cs:22:false -Member SchemeGuesses -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
