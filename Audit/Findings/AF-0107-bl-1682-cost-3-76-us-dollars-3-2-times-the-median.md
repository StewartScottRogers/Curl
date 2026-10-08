---
id: AF-0107
title: BL-1682 cost 3.76 US dollars, 3.2 times the median, over three runs
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-1682:cost-outlier
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
# AF-0107 - BL-1682 cost 3.76 US dollars, 3.2 times the median, over three runs

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261007-201432-L9.log`: BL-1682 cost 3.76 US dollars, 3.2 times the median, over three runs.

## Evidence

Location: `logs/DarkFactory-20261007-201432-L9.log`

Runs: L9 $1.79 (48 turns, measured but not fixed), L2 $0.73 (waits on BL-1715), L8 $1.24 (done). See the BL-1682 redone-work finding.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1682' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1682 claims 3 costUsd 3.7582

## Re-audits

## Log

- 2026-10-08: filed proposed.
