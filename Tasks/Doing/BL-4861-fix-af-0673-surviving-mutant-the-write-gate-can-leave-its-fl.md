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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
