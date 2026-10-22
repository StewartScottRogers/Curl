---
id: AF-0360
title: BL-3067 cost $1.13, 4.2 times the median
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-3067:cost-outlier
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
# AF-0360 - BL-3067 cost $1.13, 4.2 times the median

## Summary

Low finding from the process auditor at `logs/BL-3067-20261022-062551-L1.jsonl`: BL-3067 cost $1.13, 4.2 times the median.

## Evidence

Location: `logs/BL-3067-20261022-062551-L1.jsonl`

One run: 24 turns, 10.4 min, cache_read 1,085,696, cache_creation 97,715, output 6,766, cost 1.1343. It was claimed once; cache creation is high for its 24 turns.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3067'
```

- Expected: costUsd at most 0.8106
- Actual: claims 1, minutes 15.27, costUsd 1.1343, done

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
