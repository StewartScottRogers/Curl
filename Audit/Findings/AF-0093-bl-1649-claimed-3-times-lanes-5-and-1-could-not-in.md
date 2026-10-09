---
id: AF-0093
title: BL-1649 claimed 3 times; lanes 5 and 1 could not integrate because fast tests failed after rebasing
auditor: process
severity: Medium
status: closed
reason: Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
key: process:logs:BL-1649:redone-work
reproduction: none
task: BL-1771
tasks: BL-1771
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: reliable-reaudit
closed-by: 2026-10-09_0225.md
---
# AF-0093 - BL-1649 claimed 3 times; lanes 5 and 1 could not integrate because fast tests failed after rebasing

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261007-111121-L5.log`: BL-1649 claimed 3 times; lanes 5 and 1 could not integrate because fast tests failed after rebasing.

## Evidence

Location: `logs/DarkFactory-20261007-111121-L5.log`

L5 claimed it at 15:21:33 (14.7 min, 38 turns) and PARKED at 15:50:30: 'fast tests failed twice (Curl.Cookies.UnitTests: EveryMember_ManyConcurrentCallers_EndWit~'. L1 claimed it at 15:51:06 (7.4 min) and PARKED at 16:05:32: 'fast tests failed twice (no test named; then no test named)'. L7 claimed it at 16:05:57 and was Done at 16:12:47. Same cause as BL-1647: the flaky cookie test that BL-1651 fixed at 16:02:59.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1649' | Select-Object id,claims,outcome; Select-String -Path ..\logs\DarkFactory-20261007-111121-L*.log -Pattern 'BL-1649 (claim|PARKED|REQUEUE|DONE)'
```

- Expected: BL-1649 claimed once.
- Actual: BL-1649 claims 3; PARKED on L5 15:50:30 and L1 16:05:32; DONE on L7 16:12:47

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the reproduction. There is no BL-1649 row; DarkFactory-20261007-111121-L*.log matched no file. Could not tell.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | No lane failed fast tests after rebasing this window. BL-1818, BL-1850 and BL-1859 each integrated on their first verify, and there are no PARKED lines.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: the reproduction no longer reproduces.
