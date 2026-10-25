---
id: AF-0435
title: Lanes idled 85 minutes (39.5% of lane time) because every ready Backlog task was interactive only
auditor: process
severity: Medium
status: proposed
reason:
key: process:logs:interactive-only-backlog:empty-queue
reproduction: none
task: none
tasks:
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0435 - Lanes idled 85 minutes (39.5% of lane time) because every ready Backlog task was interactive only

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261024-185147-L1.log`: Lanes idled 85 minutes (39.5% of lane time) because every ready Backlog task was interactive only.

## Evidence

Location: `logs/DarkFactory-20261024-185147-L1.log`

waitNothingReadyMinutes=85 of laneMinutes=215.25 (39.5%, over 20%). Lanes 1, 2 and 3 logged 'No task is ready. Backlog is interactive only, waiting on Stewart or an interactive session' about once a minute: L1 from 19:13:32 to 20:17:29, then 'shift end (nothing ready)' at 20:18:35; L2 from 19:11:24 to 19:24:17; L3 from 19:10:54 to 19:23:33. The board at commit 975151e (19:13:02) held 13 Backlog tasks: BL-3363, BL-3364, BL-3365, BL-3451, BL-3452, BL-3453, BL-3454, BL-3455, BL-3456 and BL-3457 (each touches [RunDarkFactory.ps1], lane: no), BL-3464 (touches .github/workflows/ci.yml, a guard file, lane: no), BL-3468 (lane: no, requeued because the audit guard refuses Invoke-MutationTest.ps1) and BL-3472 (touches .claude, a guard path). BL-3465 was in Doing with lane: no. The coordinator logged 'Audit due: factory-script, reaudit-pending:8, interactive-stuck:1' at 20:19:03. The next shift then started twice (DarkFactory-20261024-203407.log 20:34:11 and DarkFactory-20261024-203504.log 20:35:06) and was refused both times: 'BL-3472 is in Doing and held by no lane'. Each refusal ran all four alarm stages, so no lane work followed. With lane 9's unsupported 47.6 minutes left out, the share is still about 51%.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-24 -LogRoot <log folder> -CiRunsJson <log folder>\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path <log folder>\DarkFactory-20261024-185147-L*.log -Pattern 'Backlog is interactive only' | Measure-Object
```

- Expected: waitNothingReadyMinutes 20% of laneMinutes or less.
- Actual: waitNothingReadyMinutes 85 of laneMinutes 215.25 (39.5%); the 13 Backlog tasks were all lane: no or touched guard paths.

## Re-audits

## Log

- 2026-10-24: filed proposed.
