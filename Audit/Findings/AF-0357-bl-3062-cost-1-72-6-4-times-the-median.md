---
id: AF-0357
title: BL-3062 cost $1.72, 6.4 times the median
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-3062:cost-outlier
reproduction: none
task: BL-3137
tasks: BL-3137
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0357 - BL-3062 cost $1.72, 6.4 times the median

## Summary

Low finding from the process auditor at `logs/BL-3062-20261022-024438-L1.jsonl`: BL-3062 cost $1.72, 6.4 times the median.

## Evidence

Location: `logs/BL-3062-20261022-024438-L1.jsonl`

One run: 39 turns, 10.6 min, cache_read 3,140,821, output 17,687, cost 1.7175. It was claimed once; the cost comes from cache rereads.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3062'
```

- Expected: costUsd at most 0.8106
- Actual: claims 1, minutes 11.9, costUsd 1.7175, done

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
