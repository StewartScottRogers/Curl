---
id: AF-0099
title: BL-1683 cost 4.42 US dollars, 3.7 times the median, over two runs; the first was stashed and requeued for a held project
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
key: process:logs:BL-1683:cost-outlier
reproduction: none
task: BL-1777
tasks: BL-1777
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: reliable-reaudit
closed-by: 2026-10-09_0225.md
---
# AF-0099 - BL-1683 cost 4.42 US dollars, 3.7 times the median, over two runs; the first was stashed and requeued for a held project

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261007-111121-L5.log`: BL-1683 cost 4.42 US dollars, 3.7 times the median, over two runs; the first was stashed and requeued for a held project.

## Evidence

Location: `logs/DarkFactory-20261007-111121-L5.log`

L5 claimed it at 17:53:30, ran 21.9 min and 56 turns ($2.46), then stash and REQUEUE at 18:46:58: 'Needs Curl.Console.UnitTests, held by B~'. L1 in shift 201432 claimed it at 20:14:56 and was Done at 20:36:00 after 53 turns ($1.96).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1683' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1683 claims 2 costUsd 4.4205

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the reproduction. There is no BL-1683 row; its runs are not in the copied log folder.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | No cost outlier and no stash-and-requeue in the window. The most expensive task is BL-1818 at $1.49 (median $1.09), and BL-1849's requeue cost $0.51.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
