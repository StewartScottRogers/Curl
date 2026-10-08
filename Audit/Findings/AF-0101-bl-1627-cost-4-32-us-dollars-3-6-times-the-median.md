---
id: AF-0101
title: BL-1627 cost 4.32 US dollars, 3.6 times the median; a 70-turn run on lane 7 was parked on the flaky cookie test
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-1627:cost-outlier
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
# AF-0101 - BL-1627 cost 4.32 US dollars, 3.6 times the median; a 70-turn run on lane 7 was parked on the flaky cookie test

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261007-111121-L7.log`: BL-1627 cost 4.32 US dollars, 3.6 times the median; a 70-turn run on lane 7 was parked on the flaky cookie test.

## Evidence

Location: `logs/DarkFactory-20261007-111121-L7.log`

L7 run: 13.5 min, 70 turns, $3.56, then PARKED at 16:05:48 (fast tests failed twice: Curl.Http2.UnitTests, then the EveryMember cookie test). L3 redid it at 16:41:08 in 6.6 min for $0.76.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1627' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1627 claims 2 costUsd 4.3163

## Re-audits

## Log

- 2026-10-08: filed proposed.
