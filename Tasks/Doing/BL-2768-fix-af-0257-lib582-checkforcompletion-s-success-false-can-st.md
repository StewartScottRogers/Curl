---
id: BL-2768
title: Fix AF-0257: Lib582 CheckForCompletion's success = false can start as true with no test failing, hiding 'Error uploading file.'
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-10-18
completed:
---
# BL-2768 — Fix AF-0257: Lib582 CheckForCompletion's success = false can start as true with no test failing, hiding 'Error uploading file.'

## Goal

The defect the audit office reported as AF-0257 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0257 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0257-lib582-checkforcompletion-s-success-false-can-star.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:158`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:158`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original `success = false;`, mutated `true`. When no multi message arrives, the port reports success. It then skips writing 'Error uploading file.' and setting MajorBad, which changes the libtest's result and standard error that upstream case 582 judges.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:158:false -Member CheckForCompletion -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
