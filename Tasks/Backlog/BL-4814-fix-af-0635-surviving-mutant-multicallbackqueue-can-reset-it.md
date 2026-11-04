---
id: BL-4814
title: Fix AF-0635: Surviving mutant: MultiCallbackQueue can reset its arrival signal while callbacks are still queued, and no test fails
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4814 — Fix AF-0635: Surviving mutant: MultiCallbackQueue can reset its arrival signal while callbacks are still queued, and no test fails

## Goal

The defect the audit office reported as AF-0635 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0635 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0635-surviving-mutant-multicallbackqueue-can-reset-its.md`.

Location: `Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124`

Location: `Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'if (queued.Count == 0 && arrived.Task.IsCompleted)' -> '!='. The queue resets its 'arrived' signal and queuedEvent when callbacks are still waiting and keeps them set when it is empty. A multi wait can then sleep past a queued callback (CGR-033's transfer callback marshalling) or spin on an empty queue. No MultiCallbackQueueTests or Cgr033Tests case dequeues with more than one callback queued.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124:== -Member TryDequeue -ExcludeBaselineFailures -TimeoutSeconds 600
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
