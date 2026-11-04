---
id: AF-0645
title: Lanes idle 24.2% of lane time waiting on an interactive-only Backlog
auditor: process
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: process:logs:interactive-only-backlog:empty-queue
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0645 - Lanes idle 24.2% of lane time waiting on an interactive-only Backlog

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261103-185800-L1.log:2`: Lanes idle 24.2% of lane time waiting on an interactive-only Backlog. Reappeared; previously AF-0435.

## Evidence

Location: `logs/DarkFactory-20261103-185800-L1.log:2`

waitNothingReadyMinutes=108 of laneMinutes=445.55 (24.2%, over the 20% bar); waitOverlapMinutes=0. The 185800 shift (a restart of 183829) waited from 18:58 to 19:17 on lanes L1-L7, then L8 ended 'nothing a lane can claim'. Each wait line reads 'No task is ready. Backlog is interactive only, waiting on Stewart or an interactive session'. The same message recurs in 195556 L1/L2 from 20:00 to 20:11, and the 165213 lanes waited from 16:57 to 17:02. The whole Backlog at the audited commit is interactive only. BL-4782 touches the guard file .github/workflows/ci.yml. BL-4797, BL-4798 and BL-4799 (fixes for process findings AF-0624 to AF-0626, touches RunDarkFactory.ps1) carry 'lane: no', which New-TasksFromAcceptedFindings.ps1 sets for every process finding's task (self-test AF-0014). Seven more CI-red fix tasks (BL-4647, BL-4648, BL-4649, BL-4715, BL-4716, BL-4717, BL-4778) were Blocked at 22:03 ('Criterion 1 cannot hold as written'). The coordinator reported 'Audit due: ... interactive-stuck:3' seven times (16:15 to 21:47) without any interactive session taking these tasks. So 8 restarted lanes were spun up into a queue they could not claim from.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261103-220320\logs -CiRunsJson Z:\repos\Curl.audit\20261103-220320\logs\ci-runs.json -OutFile $env:TEMP\process.json; $j = Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json; $j.waitNothingReadyMinutes / $j.laneMinutes; Select-String -Path Z:\repos\Curl.audit\20261103-220320\logs\DarkFactory-20261103-*-L*.log -Pattern 'interactive only' | Measure-Object
```

- Expected: waitNothingReadyMinutes at most 20% of laneMinutes
- Actual: 108 / 445.55 = 0.242; 46 'interactive only' wait lines

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-04: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
