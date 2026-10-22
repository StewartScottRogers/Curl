---
id: AF-0361
title: BL-3079 cost $0.93 over two claims; the first was requeued because DarkFactory/TaskBudget.ps1 was held by another task
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-3079:cost-outlier
reproduction: none
task: BL-3141
tasks: BL-3141
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0361 - BL-3079 cost $0.93 over two claims; the first was requeued because DarkFactory/TaskBudget.ps1 was held by another task

## Summary

Low finding from the process auditor at `logs/BL-3079-20261022-080746-L1.jsonl`: BL-3079 cost $0.93 over two claims; the first was requeued because DarkFactory/TaskBudget.ps1 was held by another task.

## Evidence

Location: `logs/BL-3079-20261022-080746-L1.jsonl`

First run BL-3079-20261022-062551-L3.jsonl: 11 turns, cost 0.1933, then 'REQUEUE ... DarkFactory/TaskBudget.ps1 is held by B~' at 06:51:49 on L3. Second run BL-3079-20261022-080746-L1.jsonl: 20 turns, cost 0.7392. The file overlap forced a second run.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3079'
```

- Expected: costUsd at most 0.8106
- Actual: claims 2, minutes 5.45, costUsd 0.9325, done

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
