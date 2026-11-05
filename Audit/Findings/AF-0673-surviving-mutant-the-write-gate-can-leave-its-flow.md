---
id: AF-0673
title: Surviving mutant: the write gate can leave its flow marked as holding it, so later writes skip the lock
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/WriteGate.cs:RunExclusive-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/WriteGate.cs:85:false
task: none
tasks:
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0673 - Surviving mutant: the write gate can leave its flow marked as holding it, so later writes skip the lock

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/WriteGate.cs:85`: Surviving mutant: the write gate can leave its flow marked as holding it, so later writes skip the lock. Reported by an auditor flagged unreliable in 2026-11-04_1442.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/WriteGate.cs:85`

Mutant 'heldByThisFlow.Value = false;' in RunExclusive's finally -> 'heldByThisFlow.Value = true;' survived (sample -Seed 413592). After one exclusive write the flow still believes it holds the gate, so its next RunExclusive goes through 'if (heldByThisFlow.Value) { write(); return; }' without permit.Wait(). Its writes can then interleave with another flow's writes to the shared output, and no test checks that a second exclusive write waits.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/WriteGate.cs:85:false -Member RunExclusive -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
