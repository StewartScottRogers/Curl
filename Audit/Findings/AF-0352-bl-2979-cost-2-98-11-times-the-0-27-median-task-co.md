---
id: AF-0352
title: BL-2979 cost $2.98, 11 times the $0.27 median task cost
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-2979:cost-outlier
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
# AF-0352 - BL-2979 cost $2.98, 11 times the $0.27 median task cost

## Summary

Low finding from the process auditor at `logs/BL-2979-20261022-024438-L6.jsonl`: BL-2979 cost $2.98, 11 times the $0.27 median task cost.

## Evidence

Location: `logs/BL-2979-20261022-024438-L6.jsonl`

One Opus feature-pipeline run (claimed 03:09:45 on L6, ended 03:32:11): 63 turns, 22.4 min, cache_read 5,606,826, cache_creation 154,871, output 30,945 tokens, cost 2.9796. This is the costliest task of the window. The cost comes from turns and rereading the cached context (5.6M cache-read tokens), not from retries: it was claimed once.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2979'
```

- Expected: costUsd at most 3 times the median (0.8106)
- Actual: claims 1, minutes 23.78, costUsd 2.9796, done

## Re-audits

## Log

- 2026-10-22: filed proposed.
