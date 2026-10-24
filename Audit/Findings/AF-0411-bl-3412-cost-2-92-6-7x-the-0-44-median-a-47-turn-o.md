---
id: AF-0411
title: BL-3412 cost $2.92 (6.7x the $0.44 median): a 47-turn Opus retry that ran out of cap after an earlier Opus run found an undeclared dependency
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-24_0845.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3458) are Done at d3ee244c64c5, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:BL-3412:cost-outlier
reproduction: none
task: BL-3458
tasks: BL-3458
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed: 2026-10-24
closed-how: task-done-historical
closed-by: 2026-10-24_0845.md
---
# AF-0411 - BL-3412 cost $2.92 (6.7x the $0.44 median): a 47-turn Opus retry that ran out of cap after an earlier Opus run found an undeclared dependency

## Summary

Low finding from the process auditor at `logs/BL-3412-20261023-171352-L3.jsonl`: BL-3412 cost $2.92 (6.7x the $0.44 median): a 47-turn Opus retry that ran out of cap after an earlier Opus run found an undeclared dependency.

## Evidence

Location: `logs/BL-3412-20261023-171352-L3.jsonl`

Run BL-3412-20261023-110315-L4.jsonl: opus, 25 turns, 5.7 min, $0.73, ending in REQUEUE 'Depends on BL-3410'. Run BL-3412-20261023-171352-L3.jsonl: opus retry, 47 turns, 9.6 min, $2.19, 3.73M cache-read tokens; code done but cost cap reached before the gates, work stashed. A separate split run (split-BL-3412-20261023-191239-L1.jsonl, sonnet, $0.26) was not counted. Cost drivers: one wasted Opus run on a task that was not ready, and an Opus retry rereading a large context.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5
```

- Expected: no task over 3 times the median costUsd
- Actual: BL-3412 costUsd 2.9222, top of the list; median of the 177 task costs is 0.4392

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
- 2026-10-24: accepted -> closed. Audit 2026-10-24_0845.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3458) are Done at d3ee244c64c5, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
