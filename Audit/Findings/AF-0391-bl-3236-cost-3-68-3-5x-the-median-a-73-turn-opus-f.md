---
id: AF-0391
title: BL-3236 cost $3.68 (3.5x the median): a 73-turn Opus feature run plus a rebase-conflict resolve run
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-3236:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0391 - BL-3236 cost $3.68 (3.5x the median): a 73-turn Opus feature run plus a rebase-conflict resolve run

## Summary

Low finding from the process auditor at `logs/BL-3236-20261023-035126-L2.jsonl`: BL-3236 cost $3.68 (3.5x the median): a 73-turn Opus feature run plus a rebase-conflict resolve run.

## Evidence

Location: `logs/BL-3236-20261023-035126-L2.jsonl`

Main run: Opus, 73 turns, 17.9 min, $3.32 (04:49:00 to 05:06:57). Resolve run: 14 turns, 5.6 min, $0.36. The verify step then ran all 45 test projects because an IntegrationTests project changed. Cost is driven by turn count on Opus for a differential-replay port. The task was routed to Opus as a 'feature pipeline' task.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261023-035126-L2.log -Pattern 'BL-3236 (claim|end|resolve|verify)'
```

- Expected: costUsd at most 3x the median ($3.19).
- Actual: tasks[BL-3236].costUsd = 3.681

## Re-audits

## Log

- 2026-10-23: filed proposed.
