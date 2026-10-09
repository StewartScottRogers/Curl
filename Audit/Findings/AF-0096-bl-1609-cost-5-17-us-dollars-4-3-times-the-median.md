---
id: AF-0096
title: BL-1609 cost 5.17 US dollars, 4.3 times the median, over five runs
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
key: process:logs:BL-1609:cost-outlier
reproduction: none
task: BL-1774
tasks: BL-1774
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: reliable-reaudit
closed-by: 2026-10-09_0225.md
---
# AF-0096 - BL-1609 cost 5.17 US dollars, 4.3 times the median, over five runs

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261007-201432-L1.log`: BL-1609 cost 5.17 US dollars, 4.3 times the median, over five runs.

## Evidence

Location: `logs/DarkFactory-20261007-201432-L1.log`

Five runs: L4 $0.41, L8 $1.15, L6 $2.03 (opus $1.39 + one Sonnet sub-agent $0.64), L9 $0.41, L1 $1.18. Four of the five ended in a requeue; see the BL-1609 redone-work finding.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1609' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1609 claims 5 costUsd 5.1715

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the reproduction. There is no BL-1609 row; its runs are not in the copied 2026-10-08 log folder.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | No cost outlier in the window: the most expensive task is BL-1818 at $1.49, 1.37 times the $1.09 median. BL-1850's five claims cost $0.99.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
