---
id: AF-0106
title: BL-1619 cost 3.79 US dollars, 3.2 times the median; a 66-turn run on lane 2 was parked on the flaky cookie test
auditor: process
severity: Low
status: accepted
reason:
key: process:logs:BL-1619:cost-outlier
reproduction: none
task: BL-1784
tasks: BL-1784
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0106 - BL-1619 cost 3.79 US dollars, 3.2 times the median; a 66-turn run on lane 2 was parked on the flaky cookie test

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261007-111121-L2.log`: BL-1619 cost 3.79 US dollars, 3.2 times the median; a 66-turn run on lane 2 was parked on the flaky cookie test.

## Evidence

Location: `logs/DarkFactory-20261007-111121-L2.log`

L2 run: 24.7 min, 66 turns, $3.32, then PARKED at 15:49:29 (EveryMember_ManyConcurrentCallers cookie test). L4 finished it in 2.4 min for $0.46 (DONE 16:11:03).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1619' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1619 claims 2 costUsd 3.7869

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the reproduction. There is no BL-1619 row; its runs are not in the copied log folder.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
