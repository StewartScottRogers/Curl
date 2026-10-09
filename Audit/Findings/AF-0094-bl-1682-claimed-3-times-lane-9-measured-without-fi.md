---
id: AF-0094
title: BL-1682 claimed 3 times; lane 9 measured without fixing and lane 2 found it waiting on BL-1715, which its depends-on did not name
auditor: process
severity: Medium
status: closed
reason: Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
key: process:logs:BL-1682:redone-work
reproduction: none
task: BL-1772
tasks: BL-1772
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: reliable-reaudit
closed-by: 2026-10-09_0225.md
---
# AF-0094 - BL-1682 claimed 3 times; lane 9 measured without fixing and lane 2 found it waiting on BL-1715, which its depends-on did not name

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261007-201432-L2.log`: BL-1682 claimed 3 times; lane 9 measured without fixing and lane 2 found it waiting on BL-1715, which its depends-on did not name.

## Evidence

Location: `logs/DarkFactory-20261007-201432-L2.log`

L9 claimed it at 20:51:04, ran 26.7 min and 48 turns ($1.79), and requeued at 21:20:34: 'Measured, not fixed ... more than this run's budget'. L2 claimed it at 21:33:51, ran 1.5 min ($0.73), and requeued at 21:38:14: 'Waits on BL-1715 (lazy per-scheme protocol handlers)'. A claim the board should not have made. L8 claimed it at 21:54:06 and was Done at 22:05:18 by another route (native curl.exe delay-loads DLLs). Total $3.76.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1682' | Select-Object id,claims,outcome,costUsd; Select-String -Path ..\logs\DarkFactory-20261007-201432-L*.log -Pattern 'BL-1682 (claim|REQUEUE|DONE)'
```

- Expected: BL-1682 claimed once.
- Actual: BL-1682 claims 3, costUsd 3.7582; REQUEUE on L9 21:20:34 and L2 21:38:14; DONE on L8 22:05:18

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the reproduction. There is no BL-1682 row; DarkFactory-20261007-201432-L*.log matched no file. Could not tell.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | No task was requeued for an unnamed dependency. The window's requeues are BL-1850 (Lane stalled, 3 times) and BL-1849 (Interactive only: the gap office cache, once).

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
