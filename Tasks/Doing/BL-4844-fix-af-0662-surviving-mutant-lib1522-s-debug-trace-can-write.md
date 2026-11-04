---
id: BL-4844
title: Fix AF-0662: Surviving mutant: lib1522's debug trace can write hex dumps (noHex false)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4844 — Fix AF-0662: Surviving mutant: lib1522's debug trace can write hex dumps (noHex false)

## Goal

The defect the audit office reported as AF-0662 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0662 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0662-surviving-mutant-lib1522-s-debug-trace-can-write-h.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63`

Mutant 'noHex: true' -> 'noHex: false' on its LibTestDebugTrace survived. The port's stderr trace would then carry hex dumps that upstream lib1522 does not write. No test pins that trace.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
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
