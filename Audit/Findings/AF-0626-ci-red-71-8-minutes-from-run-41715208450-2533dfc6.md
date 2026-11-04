---
id: AF-0626
title: CI red 71.8 minutes from run 41715208450 (2533dfc6): two failures whose logs the coordinator could not read, no CI task filed
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-11-03_2203.md: the reproduction no longer reproduces.
key: process:logs:41715208450:ci-red
reproduction: none
task: BL-4799
tasks: BL-4799
found: 2026-11-03
found-at: 7410f102c809d5232e4f9b210b3ac32486848c27
scorecard: 2026-11-03_1615.md
duplicate-of:
closed: 2026-11-03
closed-how: reliable-reaudit
closed-by: 2026-11-03_2203.md
---
# AF-0626 - CI red 71.8 minutes from run 41715208450 (2533dfc6): two failures whose logs the coordinator could not read, no CI task filed

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261103-145254.log:30`: CI red 71.8 minutes from run 41715208450 (2533dfc6): two failures whose logs the coordinator could not read, no CI task filed.

## Evidence

Location: `logs/DarkFactory-20261103-145254.log:30`

ciRedSpells: 2026-11-03T21:35:59Z to 22:47:47Z (14:35 to 15:47 Arizona), 71.8 min, runId 41715208450 on 2533dfc6 ('chore(tasks): file BL-4734 to BL-4778 ...'). The coordinator logged 'cannot read its log with gh run view --log-failed' for it at 14:58:53 (145254.log:30) and for run 41723085402 on 441bcff2 at 15:22:12 (145254.log:38), and filed no CI task for either. The only CI-related task in the window, BL-4780 (flaky MissedPing Socket.IO test), was filed at 15:09 from BL-4755's local run, not by the CI watch. Green returned at 15:47 on run 41729449902 (5d1b4dcf). Run 41735630431 (fad49f8f) failed again at 16:13 on IdleTimeout_ContainerAsks1000msAndWaits2500..., so CI was red again at the end of the window.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261103-161553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-161553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41715208450; Select-String -Path Z:\repos\Curl.audit\20261103-161553\logs\DarkFactory-20261103-145254.log -Pattern ' ci '
```

- Expected: No red spell over 30 minutes; each failing run's test filed as a CI fix task
- Actual: Spell 71.8 min; runs 41715208450 and 41723085402 logged as 'cannot read its log'; no task filed

## Re-audits

- 2026-11-03 | 2026-11-03_2203.md | reproduces: yes | still reported
- 2026-11-03 | 2026-11-03_2203.md | reproduces: no | The reproduction still shows the historic spell 41715208450 (71.8 min) and 145254.log lines 30 and 38 'cannot read its log'. Judged by mechanism over this window: the later failures 41735630431 and 41764281440 were read and filed as CI tasks, BL-4781 at 16:26:01 and BL-4804 at 20:31:52. Neither spell exceeded 30 min (16.15, 14.27), and no unreadable failure log recurred. 19 tasks Done.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_2203.md: the reproduction no longer reproduces.
