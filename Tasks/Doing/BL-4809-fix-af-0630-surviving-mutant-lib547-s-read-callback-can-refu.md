---
id: BL-4809
title: Fix AF-0630: Surviving mutant: lib547's read callback can refuse a buffer exactly the upload's size, and no test fails
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4809 — Fix AF-0630: Surviving mutant: lib547's read callback can refuse a buffer exactly the upload's size, and no test fails

## Goal

The defect the audit office reported as AF-0630 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0630 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0630-surviving-mutant-lib547-s-read-callback-can-refuse.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:88`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:88`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'if (buffer.Length >= UploadThis.Length)' -> '>'. With a buffer exactly the data's length, the port writes 'READ NOT FINE!' and uploads nothing instead of 'READ!' and the data. That changes the program's stderr and the request body. No test reads with a buffer of that size.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:88:>= -Member Read -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
