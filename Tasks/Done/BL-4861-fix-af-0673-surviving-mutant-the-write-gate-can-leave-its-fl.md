---
id: BL-4861
title: Fix AF-0673: Surviving mutant: the write gate can leave its flow marked as holding it, so later writes skip the lock
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4861 — Fix AF-0673: Surviving mutant: the write gate can leave its flow marked as holding it, so later writes skip the lock

## Goal

The defect the audit office reported as AF-0673 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0673 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0673-surviving-mutant-the-write-gate-can-leave-its-flow.md`.

Location: `Curl.Engine.UnitLibrary/WriteGate.cs:85`

Location: `Curl.Engine.UnitLibrary/WriteGate.cs:85`

Mutant 'heldByThisFlow.Value = false;' in RunExclusive's finally -> 'heldByThisFlow.Value = true;' survived (sample -Seed 413592). After one exclusive write the flow still believes it holds the gate, so its next RunExclusive goes through 'if (heldByThisFlow.Value) { write(); return; }' without permit.Wait(). Its writes can then interleave with another flow's writes to the shared output, and no test checks that a second exclusive write waits.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/WriteGate.cs:85:false -Member RunExclusive -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- WriteGate.cs is correct; the mutant survived for want of a test. Added
  `WriteGateTests.RunExclusive_AfterItReturns_NextWriteOfThisFlowWaitsForAnotherHolder`: after one
  `RunExclusive`, another flow (started with the context captured before it, so a leaked
  `heldByThisFlow` cannot reach it) takes the gate, and this flow's `WriteExclusiveAsync` must not
  complete or write until the holder lets go.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1` (audit paths), so the reproduction was run
  by hand: with line 85 mutated to `heldByThisFlow.Value = true;` the new test fails (mutant killed);
  restored, all 6 WriteGateTests pass. The quality auditor's re-audit confirms it with the script.
- Only a test changed, so no Measure-CodeQuality run was needed (no production code changed).
  Narrowing touches to the test file was not published (the commit and push command was refused), so
  touches stays as claimed.
- Build clean (0 warnings); fast tests green across all 115 test projects.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. A test now checks that RunExclusive lets the gate go for its flow, killing AF-0673's mutant
