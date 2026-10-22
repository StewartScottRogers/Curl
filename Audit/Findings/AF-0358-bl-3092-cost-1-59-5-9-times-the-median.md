---
id: AF-0358
title: BL-3092 cost $1.59, 5.9 times the median
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-3092:cost-outlier
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
# AF-0358 - BL-3092 cost $1.59, 5.9 times the median

## Summary

Low finding from the process auditor at `logs/BL-3092-20261022-062551-L7.jsonl`: BL-3092 cost $1.59, 5.9 times the median.

## Evidence

Location: `logs/BL-3092-20261022-062551-L7.jsonl`

One run: 46 turns, 7.6 min, cache_read 2,767,634, output 23,993, cost 1.5878. It was claimed once.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3092'
```

- Expected: costUsd at most 0.8106
- Actual: claims 1, minutes 8.92, costUsd 1.5878, done

## Re-audits

## Log

- 2026-10-22: filed proposed.
