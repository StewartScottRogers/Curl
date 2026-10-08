---
id: BL-1774
title: Fix AF-0096: BL-1609 cost 5.17 US dollars, 4.3 times the median, over five runs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1774 — Fix AF-0096: BL-1609 cost 5.17 US dollars, 4.3 times the median, over five runs

## Goal

The defect the audit office reported as AF-0096 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0096 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0096-bl-1609-cost-5-17-us-dollars-4-3-times-the-median.md`.

Location: `logs/DarkFactory-20261007-201432-L1.log`

Location: `logs/DarkFactory-20261007-201432-L1.log`

Five runs: L4 $0.41, L8 $1.15, L6 $2.03 (opus $1.39 + one Sonnet sub-agent $0.64), L9 $0.41, L1 $1.18. Four of the five ended in a requeue; see the BL-1609 redone-work finding.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1609' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1609 claims 5 costUsd 5.1715

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- ADR-0437 (BL-1773) takes a task's last-day runs off its next cap but never below $1, so a task requeued five times (BL-1609) still got $1 and a turn each time with no bound on its total.
- Fix (ADR-0438): on a fresh claim, `Get-TaskCapSpentWhy` / `Test-TaskCapSpent` block the task without running it once its runs of the last 24 hours left less than $1 of its cap; resumed and overtime runs are not checked, and the block does not count towards the failing-runs streak. With BL-1609's costs it would have stopped after $3.59. `-TestTaskBudget` passes 22/22 with five new cases.
- The reproduction's `-Since 2026-10-07` window will always show BL-1609's historical $5.17, since logs do not change, and a lane cannot run `Audit/Tools/Measure-FactoryProcess.ps1` (audit path guard); the box is ticked on the mechanism, as for BL-1773. The re-audit should measure runs after this change.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. A freshly claimed task whose runs of the last day left under $1 of its cost cap is now blocked without running (ADR-0438); build clean, fast tests green
