---
id: AF-0403
title: RunDarkFactory.ps1's -TaskBudgetUsd comment says a run that reaches the cost cap has its task filed as Blocked; the shift splits it with Invoke-TaskSplit
auditor: truthfulness
severity: High
status: accepted
reason: 
key: truthfulness:RunDarkFactory.ps1:TaskBudgetUsd:false-help
reproduction: none
task: BL-3450
tasks: BL-3450
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0403 - RunDarkFactory.ps1's -TaskBudgetUsd comment says a run that reaches the cost cap has its task filed as Blocked; the shift splits it with Invoke-TaskSplit

## Summary

High finding from the truthfulness auditor at `RunDarkFactory.ps1:439`: RunDarkFactory.ps1's -TaskBudgetUsd comment says a run that reaches the cost cap has its task filed as Blocked; the shift splits it with Invoke-TaskSplit. Reported by an auditor flagged unreliable in 2026-10-23_2322.md.

## Evidence

Location: `RunDarkFactory.ps1:439`

The parameter comment at lines 437-439: 'the cap is 4 times the median recent run, at least $3, up to this, and a run that reaches it has its task filed as Blocked.' DarkFactory/ShiftRunner.ps1:193-196 does otherwise: '$capped = $capWhy -or (-not $run.TimedOut -and (Test-BudgetSpent))' then '$split = if ($capped) { Invoke-TaskSplit $id $why ... } else { 'skipped' }'. The task is moved to Blocked only when Test-TaskSplitLeavesBlockToCaller says the split was skipped or failed. The help's own COST CAP section (lines 72-75) agrees with the code: 'one bounded split run (BL-3091, ADR-0759) files smaller tasks and defers the original. The task goes to Blocked only when that split is skipped (offered once already) or fails.' The script therefore says two different things, and an agent reading the parameter comment (what Get-Help -Parameter shows) would expect a capped task in Blocked waiting for Stewart, not split into new Backlog tasks and deferred. AF-0383 reported the same false statement in the help text, which has since been fixed; this parameter comment was not.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path RunDarkFactory.ps1, DarkFactory/ShiftRunner.ps1 -Pattern 'has its task filed as Blocked', 'if \(\$capped\) \{ Invoke-TaskSplit' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: ShiftRunner.ps1:195 only: the parameter comment describes the split, as the COST CAP help does.
- Actual: RunDarkFactory.ps1:439 and ShiftRunner.ps1:195: the comment says Blocked while the code splits the task.

## Re-audits

- 2026-10-24 | 2026-10-24_0845.md | reproduces: no | Ran the reproduction: only ShiftRunner.ps1:195 matched; 'has its task filed as Blocked' is gone. The -TaskBudgetUsd comment (RunDarkFactory.ps1:437-440) now says a run that reaches the cap gets one bounded split run that defers the original, Blocked only if the split is skipped or fails.

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
