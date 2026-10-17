---
id: AF-0241
title: BL-2310 claimed 3 times and requeued twice: each run found its prerequisites still incomplete and filed more subtasks
auditor: process
severity: Low
status: proposed
reason:
key: process:BL-2310:BL-2310:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0241 - BL-2310 claimed 3 times and requeued twice: each run found its prerequisites still incomplete and filed more subtasks

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261015-062423-L3.log:616`: BL-2310 claimed 3 times and requeued twice: each run found its prerequisites still incomplete and filed more subtasks.

## Evidence

Location: `logs/DarkFactory-20261015-062423-L3.log:616`

Measure-FactoryProcess.ps1 (-Since 2026-10-15): BL-2310 claims 3. Lane traces (local time, UTC-7): L3 claim 12:41:01, build FAIL 83 error(s), REQUEUE 12:42:52 'Engine still uses CommandLineOptions in 14 files; waits on BL-2325, BL-2338, BL-2343, BL-2350' (DarkFactory-20261015-062423-L3.log:604-616); L2 claim 14:41:26, REQUEUE 14:45:09 'in 10 files; waits on BL-2355..BL-2358' (DarkFactory-20261015-144052-L2.log:2-16); L1 claim 16:20:31, DONE 16:26:41 (DarkFactory-20261015-162014-L1.log:2-20). Run costs $0.52 (21 turns), $1.08 (26 turns), $1.22 (27 turns): the first two runs ($1.60) produced only a requeue. Why: an umbrella task ('remove the engine's reference to Curl.Cli') became ready before its remaining work was split out; its depends-on list grew from the first requeue to 18 tasks, and each claim discovered the next layer of remaining files instead of the planner filing all of them up front. The task Log agrees with the run logs.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-15 -LogRoot Z:\repos\Curl.audit\20261016-162347\logs -CiRunsJson Z:\repos\Curl.audit\20261016-162347\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2310'; Select-String -Path Z:\repos\Curl.audit\20261016-162347\logs\DarkFactory-*-L*.log -Pattern 'BL-2310 (claim|REQUEUE|DONE)'
```

- Expected: BL-2310 claimed once or twice, never requeued twice.
- Actual: BL-2310 claims 3; two REQUEUE lines (12:42:52 and 14:45:09) before DONE at 16:26:41.

## Re-audits

## Log

- 2026-10-16: filed proposed.
