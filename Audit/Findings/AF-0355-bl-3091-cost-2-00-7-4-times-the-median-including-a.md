---
id: AF-0355
title: BL-3091 cost $2.00, 7.4 times the median, including a second run to resolve a rebase conflict
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-3091:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0355 - BL-3091 cost $2.00, 7.4 times the median, including a second run to resolve a rebase conflict

## Summary

Low finding from the process auditor at `logs/BL-3091-20261022-062551-L5.jsonl`: BL-3091 cost $2.00, 7.4 times the median, including a second run to resolve a rebase conflict.

## Evidence

Location: `logs/BL-3091-20261022-062551-L5.jsonl`

The main run (L5, 06:57:59-07:08:46) took 36 turns, 10.8 min, cache_read 2,443,684, output 32,353, cost 1.7365. A second run, BL-3091-20261022-062551-L5-resolve.jsonl, took 13 turns, 4.5 min and cost 0.2660 (end 07:13:22), so a rebase conflict added about 15% to the cost. Task: split a task that hit its cost cap.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3091'
```

- Expected: costUsd at most 0.8106
- Actual: claims 1, minutes 17.88, costUsd 2.0025, done

## Re-audits

## Log

- 2026-10-22: filed proposed.
