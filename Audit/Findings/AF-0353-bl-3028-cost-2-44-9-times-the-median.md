---
id: AF-0353
title: BL-3028 cost $2.44, 9 times the median
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-3028:cost-outlier
reproduction: none
task: BL-3133
tasks: BL-3133
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0353 - BL-3028 cost $2.44, 9 times the median

## Summary

Low finding from the process auditor at `logs/BL-3028-20261022-014530-L1.jsonl`: BL-3028 cost $2.44, 9 times the median.

## Evidence

Location: `logs/BL-3028-20261022-014530-L1.jsonl`

One Opus run (L1, 01:46:01-01:59:16): 60 turns, 13.2 min, cache_read 3,389,432, output 40,972 tokens, cost 2.4371. Task: make the CI watch file job-step failures with real touches (the BL-3026 empty-touches fix). The cost comes from turn count and cache rereads; there were no retries.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3028'
```

- Expected: costUsd at most 0.8106
- Actual: claims 1, minutes 14.17, costUsd 2.4371, done

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
