---
id: AF-0394
title: BL-3215 cost $3.29 (3.1x the median): a 67-turn, 48-minute Opus run plus a rebase-conflict resolve run
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-23_2322.md: the reproduction no longer reproduces.
key: process:logs:BL-3215:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed: 2026-10-23
closed-how: reliable-reaudit
closed-by: 2026-10-23_2322.md
---
# AF-0394 - BL-3215 cost $3.29 (3.1x the median): a 67-turn, 48-minute Opus run plus a rebase-conflict resolve run

## Summary

Low finding from the process auditor at `logs/BL-3215-20261022-192710-L2.jsonl`: BL-3215 cost $3.29 (3.1x the median): a 67-turn, 48-minute Opus run plus a rebase-conflict resolve run.

## Evidence

Location: `logs/BL-3215-20261022-192710-L2.jsonl`

Main run: Opus, 67 turns, 47.7 min, $3.07 (23:27:19 to 00:15:02). The trace shows repeated partial test passes (5/12, 6/12), then a 29-minute gap between test runs (23:40:18 to 00:09:28). Resolve run: 9 turns, $0.22. Task: reject a revoked server certificate on Windows with exit 35.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261022-192710-L2.log -Pattern 'BL-3215 (claim|test|end|resolve)'
```

- Expected: costUsd at most 3x the median ($3.19).
- Actual: tasks[BL-3215].costUsd = 3.2925

## Re-audits

- 2026-10-23 | 2026-10-23_2322.md | reproduces: no | Mechanism over this window: no cost outlier is made up of an Opus run plus a resolve run. The window's resolve runs (BL-3352, BL-3395, BL-3396) were sonnet, $0.10-$0.12, and their tasks are under the $1.32 3x line.

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> closed. Re-audit 2026-10-23_2322.md: the reproduction no longer reproduces.
