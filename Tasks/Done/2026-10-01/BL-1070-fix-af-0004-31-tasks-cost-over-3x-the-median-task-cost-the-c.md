---
id: BL-1070
title: Fix AF-0004: 31 tasks cost over 3x the median task cost; the costliest are BL-703, BL-527, BL-568, BL-658 and BL-708
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-30
completed: 2026-10-01
---
# BL-1070 — Fix AF-0004: 31 tasks cost over 3x the median task cost; the costliest are BL-703, BL-527, BL-568, BL-658 and BL-708

## Goal

The defect the audit office reported as AF-0004 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0004 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0004-31-tasks-cost-over-3x-the-median-task-cost-the-cos.md`.

Location: `logs/BL-703-*.jsonl`

Location: `logs/BL-703-*.jsonl`

The median cost of done tasks is about $2.36, so the 3x bar is about $7.08. The five costliest tasks are BL-703 ($14.33, 36 minutes), BL-527 ($13.83, 40 minutes), BL-568 ($12.95, 57 minutes), BL-658 ($12.58, 31 minutes) and BL-708 ($11.93, claimed twice, 27 minutes). Total cost is $1524.54, or $3.01 per task done. The causes (turns, retries, rereading) were not traced in the run logs.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-23 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5 id,costUsd
```

- Expected: No task over 3 times the median cost.
- Actual: BL-703 14.33, BL-527 13.83, BL-568 12.95, BL-658 12.58, BL-708 11.93, against a median of about 2.36.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] Every headless run `RunDarkFactory.ps1` starts is capped with `--max-budget-usd` at `-TaskBudgetUsd` (default 6, under 3x the median), so the finding's reproduction run with `-Since` on or after the day this lands finds no task run over the bar; a run that reaches the cap blocks its task for splitting (ADR-0288).
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Criterion 1 reworded (decided by Claude): as filed it ran the reproduction with
  `-Since 2026-09-23`, which reads logs of runs already finished; no change can lower
  their cost, so it could never pass. It now states what the fix guarantees for runs
  from here on. A lane may not run `Audit/Tools/Measure-FactoryProcess.ps1` (audit
  path guard), so the re-audit by the process auditor is the confirmation, as the
  finding requires.
- Measured `claude -p --max-budget-usd 0.0001` on 2026-10-01: the result event has
  `subtype` `error_max_budget_usd`, `terminal_reason` `budget_exhausted`, `is_error`
  true, and the run ended at $0.057 - the cap is checked between turns, hence $6 not $7.
- Change: `-TaskBudgetUsd` parameter (forwarded to lanes and `-Continuous` shifts),
  `--max-budget-usd` in `Invoke-TaskRun`, `Test-BudgetSpent`, and a Blocked reason
  "stopped at its N US dollar cost cap (-TaskBudgetUsd), so split the task"; header
  section COST CAP. Script parses; `-TestTaskIds` passes. Build clean, fast tests green
  (33 test projects, 0 failed).
- A task resumed after the usage limit gets a fresh cap per run; recorded in ADR-0288.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Dark factory runs stop at a $6 cost cap (-TaskBudgetUsd) and block the task for splitting
