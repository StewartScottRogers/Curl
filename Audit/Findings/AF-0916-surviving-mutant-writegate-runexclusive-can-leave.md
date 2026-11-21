---
id: AF-0916
title: Surviving mutant: WriteGate.RunExclusive can leave its flow marked as holding the gate, so later writes bypass it
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/WriteGate.cs:RunExclusive-true:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/WriteGate.cs:80:true
task: none
tasks:
found: 2026-11-20
found-at: e78a1e70bbba7541cf5f1abfedb0ef1c8ae8e2a0
scorecard: 2026-11-20_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0916 - Surviving mutant: WriteGate.RunExclusive can leave its flow marked as holding the gate, so later writes bypass it

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/WriteGate.cs:80`: Surviving mutant: WriteGate.RunExclusive can leave its flow marked as holding the gate, so later writes bypass it.

## Evidence

Location: `Curl.Engine.UnitLibrary/WriteGate.cs:80`

In RunExclusive's finally block `heldByThisFlow.Value = false;` mutated to `= true;` survived Curl.Engine.UnitTests. After one exclusive synchronous write the flow believes it still holds the permit, so every later WriteExclusiveAsync/RunExclusive in that flow writes straight to the shared stream without taking the permit: parallel transfers (-Z) can interleave their output bytes. No test runs an exclusive write and then checks that a later write from the same flow still waits for the gate.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/WriteGate.cs:80:true -Member RunExclusive -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-20: filed proposed.
