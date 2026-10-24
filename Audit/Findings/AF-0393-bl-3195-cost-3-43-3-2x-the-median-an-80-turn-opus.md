---
id: AF-0393
title: BL-3195 cost $3.43 (3.2x the median): an 80-turn Opus run plus two rebase-conflict resolve runs
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-23_2322.md: the reproduction no longer reproduces.
key: process:logs:BL-3195:cost-outlier
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
# AF-0393 - BL-3195 cost $3.43 (3.2x the median): an 80-turn Opus run plus two rebase-conflict resolve runs

## Summary

Low finding from the process auditor at `logs/BL-3195-20261022-192710-L2.jsonl`: BL-3195 cost $3.43 (3.2x the median): an 80-turn Opus run plus two rebase-conflict resolve runs.

## Evidence

Location: `logs/BL-3195-20261022-192710-L2.jsonl`

Main run: Opus, 80 turns, 33.3 min, $3.17 (21:25:08 to 21:58:29). Two resolve runs followed, at 21:58:32 (12 turns) and 22:04:45 (11 turns), about $0.71 together by the jsonl results. Each one rebuilt and retested after conflicts with lanes integrating Engine work at the same time.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261022-192710-L2.log -Pattern 'BL-3195 (claim|end|resolve)'
```

- Expected: costUsd at most 3x the median ($3.19).
- Actual: tasks[BL-3195].costUsd = 3.4325

## Re-audits

- 2026-10-23 | 2026-10-23_2322.md | reproduces: no | Mechanism over this window: no cost outlier includes resolve runs. The window's three resolve runs were cheap sonnet runs ($0.10-$0.12) on tasks under 3x the median. The long-Opus-run part recurs and is recorded under AF-0392.

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> closed. Re-audit 2026-10-23_2322.md: the reproduction no longer reproduces.
