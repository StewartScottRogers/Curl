---
id: BL-4814
title: Fix AF-0635: Surviving mutant: MultiCallbackQueue can reset its arrival signal while callbacks are still queued, and no test fails
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests/MultiCallbackQueueTests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was right and only a test was missing. Added `MultiCallbackQueueTests.RunQueued_CallbackQueuedWhileAnotherRuns_ResetsArrivedOnlyOnceTheQueueIsEmpty`. In it, the first callback (run by `RunQueued`) queues a second one and waits until it is queued, so each dequeue empties the queue. The test then checks that `Arrived` is incomplete once both have run. The `!=` mutant never resets, so `Arrived` stays complete and the test fails. The test does not depend on timing.
- Checked by applying the mutant (`queued.Count != 0`) to `MultiCallbackQueue.cs` by hand. The new test failed ("Expected condition to be false") and the other 8 `MultiCallbackQueueTests` passed. The change was then reverted. Lane rules forbid a lane from running the audit office's mutation script, so this manual mutation stands in for its reproduction. The quality auditor's re-audit closes the finding.
- Widened `touches` to `Curl.Engine.UnitTests/MultiCallbackQueueTests.cs`, because the test belongs there. No task in Doing on origin/work/dark-factory names that file. Production code is unchanged, so `Measure-CodeQuality.ps1` was not run.
- `dotnet build` of `Curl.Engine.UnitTests` is clean, and its fast tests pass: 2972 passed, 7 skipped. The change is test-only and confined to that one project, so the rest of the solution was not rebuilt or re-tested.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. MultiCallbackQueueTests now kills the AF-0635 mutant: a callback queued while another runs resets Arrived only once the queue is empty
