---
id: BL-3450
title: Fix AF-0403: RunDarkFactory.ps1's -TaskBudgetUsd comment says a run that reaches the cost cap has its task filed as Blocked; the shift splits it with Invoke-TaskSplit
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-24
completed:
---
# BL-3450 — Fix AF-0403: RunDarkFactory.ps1's -TaskBudgetUsd comment says a run that reaches the cost cap has its task filed as Blocked; the shift splits it with Invoke-TaskSplit

## Goal

The defect the audit office reported as AF-0403 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0403 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0403-rundarkfactory-ps1-s-taskbudgetusd-comment-says-a.md`.

Location: `RunDarkFactory.ps1:439`

Location: `RunDarkFactory.ps1:439`

The parameter comment at lines 437-439: 'the cap is 4 times the median recent run, at least $3, up to this, and a run that reaches it has its task filed as Blocked.' DarkFactory/ShiftRunner.ps1:193-196 does otherwise: '$capped = $capWhy -or (-not $run.TimedOut -and (Test-BudgetSpent))' then '$split = if ($capped) { Invoke-TaskSplit $id $why ... } else { 'skipped' }'. The task is moved to Blocked only when Test-TaskSplitLeavesBlockToCaller says the split was skipped or failed. The help's own COST CAP section (lines 72-75) agrees with the code: 'one bounded split run (BL-3091, ADR-0759) files smaller tasks and defers the original. The task goes to Blocked only when that split is skipped (offered once already) or fails.' The script therefore says two different things, and an agent reading the parameter comment (what Get-Help -Parameter shows) would expect a capped task in Blocked waiting for Stewart, not split into new Backlog tasks and deferred. AF-0383 reported the same false statement in the help text, which has since been fixed; this parameter comment was not.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path RunDarkFactory.ps1, DarkFactory/ShiftRunner.ps1 -Pattern 'has its task filed as Blocked', 'if \(\$capped\) \{ Invoke-TaskSplit' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: ShiftRunner.ps1:195 only: the parameter comment describes the split, as the COST CAP help does.
- Actual: RunDarkFactory.ps1:439 and ShiftRunner.ps1:195: the comment says Blocked while the code splits the task.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
