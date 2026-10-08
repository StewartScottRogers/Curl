---
id: AF-0103
title: BL-1632 cost 3.94 US dollars, 3.3 times the median; a 73-turn run on lane 6 could not integrate
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-1632:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0103 - BL-1632 cost 3.94 US dollars, 3.3 times the median; a 73-turn run on lane 6 could not integrate

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261007-111121-L6.log`: BL-1632 cost 3.94 US dollars, 3.3 times the median; a 73-turn run on lane 6 could not integrate.

## Evidence

Location: `logs/DarkFactory-20261007-111121-L6.log`

L6 run: 16.1 min, 73 turns, $3.00, then PARKED at 15:28:19 (fast tests failed twice: Curl.Protocol.Smtp.UnitTests, then no test named). L2 redid it in 7.5 min for $0.94 (DONE 16:21:24).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1632' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1632 claims 2 costUsd 3.9408

## Re-audits

## Log

- 2026-10-08: filed proposed.
