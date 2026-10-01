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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
