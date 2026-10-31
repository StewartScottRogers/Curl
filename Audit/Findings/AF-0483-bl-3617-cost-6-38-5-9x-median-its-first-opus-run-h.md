---
id: AF-0483
title: BL-3617 cost $6.38 (5.9x median): its first opus run hit the 80-turn cap, the second took 68 turns
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-31_0358.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-4236) are Done at bfe08061f844, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-3617:cost-outlier
reproduction: none
task: BL-4236
tasks: BL-4236
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed: 2026-10-31
closed-how: task-done-historical
closed-by: 2026-10-31_0358.md
---
# AF-0483 - BL-3617 cost $6.38 (5.9x median): its first opus run hit the 80-turn cap, the second took 68 turns

## Summary

Low finding from the process auditor at `logs/BL-3617-20261026-063535-L5.jsonl`: BL-3617 cost $6.38 (5.9x median): its first opus run hit the 80-turn cap, the second took 68 turns.

## Evidence

Location: `logs/BL-3617-20261026-063535-L5.jsonl`

Run L5 on 2026-10-26, 13:03:08 to 13:32:11: 81 turns in 29 min, requeued at 13:36:17 on the 80-turn cap (DarkFactory-20261026-063535-L5.log:308). The retry on L1, 13:40:44 to 14:41:17, took 68 turns and 60.5 min and finished Done. The capped run's work was redone by the retry.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-3617'
```

- Expected: No task over 3x the median costUsd and over 5 USD.
- Actual: BL-3617 costUsd 6.3804, claims 2, outcome done.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
- 2026-10-31: accepted -> closed. Audit 2026-10-31_0358.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-4236) are Done at bfe08061f844, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
