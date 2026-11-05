---
id: BL-4871
title: Fix AF-0680: Surviving mutant: lib2700's debug trace can drop its timestamps or write hex dumps unnoticed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-05
completed:
---
# BL-4871 — Fix AF-0680: Surviving mutant: lib2700's debug trace can drop its timestamps or write hex dumps unnoticed

## Goal

The defect the audit office reported as AF-0680 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0680 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0680-surviving-mutant-lib2700-s-debug-trace-can-drop-it.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2700.cs:63`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2700.cs:63`

Line 63: 'var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);'. Both mutants of this line survive. The seed-843793 sample flipped noHex to false and it survived. The targeted -Site run flips the line's first true (traceTime) to false and it also survived. No Lib2700 test pins the trace format the port writes to standard error, which upstream's test2700 compares.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib2700.cs:63:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant is killed.
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
