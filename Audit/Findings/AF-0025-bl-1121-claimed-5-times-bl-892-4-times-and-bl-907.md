---
id: AF-0025
title: BL-1121 claimed 5 times, BL-892 4 times and BL-907 3 times
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-1121:redone-work
task: BL-1281
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0025 - BL-1121 claimed 5 times, BL-892 4 times and BL-907 3 times

## Summary

Medium finding from the process auditor at `logs/BL-1121`: BL-1121 claimed 5 times, BL-892 4 times and BL-907 3 times. Reported by an auditor flagged unreliable in 2026-10-02_1400.md.

## Evidence

Location: `logs/BL-1121`

Measure-FactoryProcess.ps1 reports claims=5 for BL-1121 (26.82 min, done), claims=4 for BL-892 (12.85 min) and claims=3 for BL-907 (20.23 min). The rule threshold is 3 or more claims. In total 10 tasks were claimed more than once and requeues=11. The reasons were not investigated.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-30 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object claims -ge 3 | Select-Object id,claims
```

- Expected: No task claimed 3 or more times.
- Actual: BL-1121 5, BL-892 4, BL-907 3

## Re-audits

- 2026-10-03 | 2026-10-03_0623.md | reproduces: no | Ran the reproduction with -Since 2026-09-30. Only BL-1289 has claims>=3 (6). BL-1121, BL-892 and BL-907 are absent: the log folder holds no logs for them. Absence of logs, not a fix.
- 2026-10-03 | 2026-10-03_1233.md | reproduces: no | With -Since 2026-09-30 the only task with claims of 3 or more is BL-1322 (5). BL-1121, BL-892 and BL-907 are absent from the logs.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
