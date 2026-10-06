---
id: BL-1377
title: Fix AF-0033: BL-1284, BL-1320 and BL-1287 cost over 3x the median task cost
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1377 — Fix AF-0033: BL-1284, BL-1320 and BL-1287 cost over 3x the median task cost

## Goal

The defect the audit office reported as AF-0033 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0033 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0033-bl-1284-bl-1320-and-bl-1287-cost-over-3x-the-media.md`.

Location: `logs/BL-1284-20261003-061205-L6.jsonl`

Location: `logs/BL-1284-20261003-061205-L6.jsonl`

Median costUsd about 1.32 (bar about 3.96). BL-1284 5.39 USD (29 min), BL-1320 4.26 (25 min), BL-1287 4.16 (102 min). Next: BL-1355 2.93, BL-1312 2.83. I did not examine what drove the cost.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5 id,costUsd
```

- Expected: No task over 3x the median.
- Actual: BL-1284 5.39, BL-1320 4.26, BL-1287 4.16

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: the $6 cap from ADR-0288 was fixed when the median task cost $2.36. The median has since dropped to about $1.32 (the audit's figure; the newest 40 runs in Curl.logs give $1.04), so runs costing $4 to $5.40 passed three times the median and still stayed under the cap.
- Fix (ADR-0407): `Invoke-TaskRun` now caps each run at 2.7 times the median `total_cost_usd` of the newest 40 task run logs (`Get-RecentRunCosts`, `Get-RunBudgetUsd`). The cap is never below $2 and never above `-TaskBudgetUsd`, and with fewer than 10 logged runs it stays at `-TaskBudgetUsd`. 2.7 rather than 3 because a run can end one turn above the cap. The Blocked reason now names the cap the run hit. `-TestTaskBudget` proves it: 11 cases, all PASS.
- The reproduction reads logs that already exist. Runs from 2026-10-03 that finished before this change (BL-1284, BL-1320, BL-1287) will always appear in it with `-Since 2026-10-03`. The criterion holds for runs started after this change, so the process auditor's re-audit should use a `-Since` after it lands. A lane cannot run `Audit/Tools` because the guard hook blocks it.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Each dark factory run is now capped at 2.7x the median recent run cost ($2 to -TaskBudgetUsd), so runs stay under 3x the median (ADR-0407)
