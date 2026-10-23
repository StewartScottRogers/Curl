---
id: AF-0383
title: RunDarkFactory.ps1 help says a run that reaches its cost cap sends its task to Blocked for Stewart; the shift splits it with Invoke-TaskSplit instead
auditor: truthfulness
severity: High
status: accepted
reason: 
key: truthfulness:RunDarkFactory.ps1:COST-CAP-help:false-help
reproduction: none
task: BL-3360
tasks: BL-3360
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0383 - RunDarkFactory.ps1 help says a run that reaches its cost cap sends its task to Blocked for Stewart; the shift splits it with Invoke-TaskSplit instead

## Summary

High finding from the truthfulness auditor at `RunDarkFactory.ps1:70`: RunDarkFactory.ps1 help says a run that reaches its cost cap sends its task to Blocked for Stewart; the shift splits it with Invoke-TaskSplit instead. Reported by an auditor flagged unreliable in 2026-10-23_0618.md.

## Evidence

Location: `RunDarkFactory.ps1:70`

Help, COST CAP section, lines 68-70: 'A task run that reaches the cap stops; like a timed-out run, its partial work is stashed and the task goes to Blocked for Stewart, since it is too big for one run and wants splitting.' The help (lines 1-426) never mentions the split run. Code, DarkFactory/ShiftRunner.ps1:192-195: '# A task stopped by its cost cap is split by one bounded run (BL-3091), which defers or blocks it itself' then '$split = if ($capped) { Invoke-TaskSplit $id $why ... }'. The task is moved to Blocked only when Test-TaskSplitLeavesBlockToCaller is true, which happens for 'skipped' (already offered for a split today) or 'failed'. DarkFactory/TaskStash.ps1:241-248 documents that a successful split files new tasks and defers the original ('split'). An agent reading the help expects capped tasks in Blocked waiting for Stewart, but they actually turn into new Backlog tasks and a Deferred original. Phase 2: explained by ADR-0759 (a task over its cost cap is split by one bounded run, BL-3091); the help was not updated when that decision landed.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path RunDarkFactory.ps1, DarkFactory/ShiftRunner.ps1 -Pattern 'goes to Blocked for Stewart, since it is too big', 'if \(\$capped\) \{ Invoke-TaskSplit' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Only ShiftRunner.ps1:195; the help instead describes the split run that defers the capped task and files smaller ones, blocking only when the split is skipped or fails.
- Actual: RunDarkFactory.ps1:70 and ShiftRunner.ps1:195: the help says Blocked while the code splits.

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> accepted.
