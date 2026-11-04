---
id: AF-0635
title: Surviving mutant: MultiCallbackQueue can reset its arrival signal while callbacks are still queued, and no test fails
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:TryDequeue-eq:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124:==
task: BL-4814
tasks: BL-4814
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0635 - Surviving mutant: MultiCallbackQueue can reset its arrival signal while callbacks are still queued, and no test fails

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124`: Surviving mutant: MultiCallbackQueue can reset its arrival signal while callbacks are still queued, and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'if (queued.Count == 0 && arrived.Task.IsCompleted)' -> '!='. The queue resets its 'arrived' signal and queuedEvent when callbacks are still waiting and keeps them set when it is empty. A multi wait can then sleep past a queued callback (CGR-033's transfer callback marshalling) or spin on an empty queue. No MultiCallbackQueueTests or Cgr033Tests case dequeues with more than one callback queued.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124:== -Member TryDequeue -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-04: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
