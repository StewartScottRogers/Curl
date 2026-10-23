---
id: BL-3360
title: Fix AF-0383: RunDarkFactory.ps1 help says a run that reaches its cost cap sends its task to Blocked for Stewart; the shift splits it with Invoke-TaskSplit instead
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3360 — Fix AF-0383: RunDarkFactory.ps1 help says a run that reaches its cost cap sends its task to Blocked for Stewart; the shift splits it with Invoke-TaskSplit instead

## Goal

The defect the audit office reported as AF-0383 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0383 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0383-rundarkfactory-ps1-help-says-a-run-that-reaches-it.md`.

Location: `RunDarkFactory.ps1:70`

Location: `RunDarkFactory.ps1:70`

Help, COST CAP section, lines 68-70: 'A task run that reaches the cap stops; like a timed-out run, its partial work is stashed and the task goes to Blocked for Stewart, since it is too big for one run and wants splitting.' The help (lines 1-426) never mentions the split run. Code, DarkFactory/ShiftRunner.ps1:192-195: '# A task stopped by its cost cap is split by one bounded run (BL-3091), which defers or blocks it itself' then '$split = if ($capped) { Invoke-TaskSplit $id $why ... }'. The task is moved to Blocked only when Test-TaskSplitLeavesBlockToCaller is true, which happens for 'skipped' (already offered for a split today) or 'failed'. DarkFactory/TaskStash.ps1:241-248 documents that a successful split files new tasks and defers the original ('split'). An agent reading the help expects capped tasks in Blocked waiting for Stewart, but they actually turn into new Backlog tasks and a Deferred original. Phase 2: explained by ADR-0759 (a task over its cost cap is split by one bounded run, BL-3091); the help was not updated when that decision landed.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path RunDarkFactory.ps1, DarkFactory/ShiftRunner.ps1 -Pattern 'goes to Blocked for Stewart, since it is too big', 'if \(\$capped\) \{ Invoke-TaskSplit' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Only ShiftRunner.ps1:195; the help instead describes the split run that defers the capped task and files smaller ones, blocking only when the split is skipped or fails.
- Actual: RunDarkFactory.ps1:70 and ShiftRunner.ps1:195: the help says Blocked while the code splits.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Rewrote the COST CAP help paragraph to describe the bounded split run (BL-3091, ADR-0759); Blocked only when the split is skipped or fails. Reproduction now matches only ShiftRunner.ps1:195.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-23: Doing -> Done. RunDarkFactory.ps1 help now describes the cost-cap split run
