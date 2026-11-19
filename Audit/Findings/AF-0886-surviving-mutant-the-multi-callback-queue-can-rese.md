---
id: AF-0886
title: Surviving mutant: the multi callback queue can reset its arrival signal while callbacks are still queued
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:TryDequeue-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124:&&
task: BL-5557
tasks: BL-5557
found: 2026-11-19
found-at: 6e68cb60b626d421113f6d7d7f35c2a46bbc3b46
scorecard: 2026-11-19_0602.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0886 - Surviving mutant: the multi callback queue can reset its arrival signal while callbacks are still queued

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124`: Surviving mutant: the multi callback queue can reset its arrival signal while callbacks are still queued.

## Evidence

Location: `Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124`

Mutant if (queued.Count == 0 && arrived.Task.IsCompleted) -> if (queued.Count == 0 || arrived.Task.IsCompleted) in TryDequeue survived all Curl.Engine.UnitTests (seed 446316). With it, taking one callback while others are still queued replaces the completed arrived signal and resets queuedEvent, so a perform waiting on the signal does not wake for the callbacks left in the queue until another one is enqueued. That decides when a transfer's callbacks run, and can stall them. MultiCallbackQueueTests never queue two callbacks before one is taken.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124:&& -Member TryDequeue -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-19: filed proposed.
