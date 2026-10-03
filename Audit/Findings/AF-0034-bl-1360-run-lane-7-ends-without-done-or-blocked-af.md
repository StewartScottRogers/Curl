---
id: AF-0034
title: BL-1360 run (lane 7) ends without DONE or BLOCKED after 87 minutes; BL-1325 blocked after 120 minutes
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-1360:unfinished-run
task: BL-1375
found: 2026-10-03
found-at: d1db9881553d55cd92c0e74bb41561c3dea9ea84
scorecard: 2026-10-03_1233.md
closed:
closed-by:
---
# AF-0034 - BL-1360 run (lane 7) ends without DONE or BLOCKED after 87 minutes; BL-1325 blocked after 120 minutes

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261003-061205-L7.log`: BL-1360 run (lane 7) ends without DONE or BLOCKED after 87 minutes; BL-1325 blocked after 120 minutes. Reported by an auditor flagged unreliable in 2026-10-03_1233.md.

## Evidence

Location: `logs/DarkFactory-20261003-061205-L7.log`

Tool outcome 'open' for BL-1360 (87.72 min, cost 0); L7's last line at 12:20:35 is a quality step. BL-1325 outcome 'blocked' (120.57 min, cost 0). I did not find the cause.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object outcome -ne 'done' | Select-Object id,outcome,minutes
```

- Expected: No open runs.
- Actual: BL-1360 open, BL-1325 blocked

## Re-audits

- 2026-10-03 | 2026-10-03_1459.md | reproduces: yes | Partly. BL-1325 is blocked at 120.57 min, as the finding says. BL-1360 is now reported as blocked at 120.25 min (timeout), not as ending without DONE or BLOCKED after 87 min. BL-1374 and BL-1335 show outcome open (50.78 and 49.43 min) in the later shift's still-running logs.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
