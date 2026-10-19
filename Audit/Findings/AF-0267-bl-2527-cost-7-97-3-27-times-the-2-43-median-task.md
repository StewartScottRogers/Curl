---
id: AF-0267
title: BL-2527 cost $7.97, 3.27 times the $2.43 median task cost: a 94-turn, 34-minute run plus a rebase-conflict resolve run
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-18_1905.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-2778) are Done at e17dfce54cf6, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-2527:cost-outlier
reproduction: none
task: BL-2778
tasks: BL-2778
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed: 2026-10-18
closed-how: task-done-historical
closed-by: 2026-10-18_1905.md
---
# AF-0267 - BL-2527 cost $7.97, 3.27 times the $2.43 median task cost: a 94-turn, 34-minute run plus a rebase-conflict resolve run

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261017-021554-L5.log:49`: BL-2527 cost $7.97, 3.27 times the $2.43 median task cost: a 94-turn, 34-minute run plus a rebase-conflict resolve run.

## Evidence

Location: `logs/DarkFactory-20261017-021554-L5.log:49`

Measure-FactoryProcess.ps1: BL-2527 claims 1, 39.27 min, $7.9658; median costUsd over 137 tasks is $2.4326 (3x = $7.30). Run BL-2527-20261017-021554-L5.jsonl: 94 turns, 2,045,880 ms, $7.49 (60 Bash, 21 Write, 10 Edit) to port libtest programs 500 to 509. Then BL-2527-20261017-021554-L5-resolve.jsonl: 'rebase conflict; resolving' at 02:53:50 (line 51), 11 turns, $0.47. Cost came from the task's size (ten programs in one run) and a 9-lane shift contending for the shared ratchet file. The other costliest tasks in the window are BL-2550 $7.39, BL-2549 $6.96, BL-2535 $6.53 and BL-2536 $6.44, all libtest port batches.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5
```

- Expected: No task over 3 times the median costUsd ($7.30).
- Actual: BL-2527 1 39.27 7.9658 done (top of the list).

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
- 2026-10-18: accepted -> closed. Audit 2026-10-18_1905.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-2778) are Done at e17dfce54cf6, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
