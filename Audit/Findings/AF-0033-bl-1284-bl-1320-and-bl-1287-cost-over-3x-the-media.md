---
id: AF-0033
title: BL-1284, BL-1320 and BL-1287 cost over 3x the median task cost
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:cost-outliers:cost-outlier
task: none
found: 2026-10-03
found-at: d1db9881553d55cd92c0e74bb41561c3dea9ea84
scorecard: 2026-10-03_1233.md
closed:
closed-by:
---
# AF-0033 - BL-1284, BL-1320 and BL-1287 cost over 3x the median task cost

## Summary

Low finding from the process auditor at `logs/BL-1284-20261003-061205-L6.jsonl`: BL-1284, BL-1320 and BL-1287 cost over 3x the median task cost. Reported by an auditor flagged unreliable in 2026-10-03_1233.md.

## Evidence

Location: `logs/BL-1284-20261003-061205-L6.jsonl`

Median costUsd about 1.32 (bar about 3.96). BL-1284 5.39 USD (29 min), BL-1320 4.26 (25 min), BL-1287 4.16 (102 min). Next: BL-1355 2.93, BL-1312 2.83. I did not examine what drove the cost.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5 id,costUsd
```

- Expected: No task over 3x the median.
- Actual: BL-1284 5.39, BL-1320 4.26, BL-1287 4.16

## Re-audits

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
