---
id: AF-0484
title: BL-3659 cost $5.48 (5.1x median): its first opus run hit the 80-turn cap
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-31_0358.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-4237) are Done at bfe08061f844, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-3659:cost-outlier
reproduction: none
task: BL-4237
tasks: BL-4237
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed: 2026-10-31
closed-how: task-done-historical
closed-by: 2026-10-31_0358.md
---
# AF-0484 - BL-3659 cost $5.48 (5.1x median): its first opus run hit the 80-turn cap

## Summary

Low finding from the process auditor at `logs/BL-3659-20261026-151632-L4.jsonl`: BL-3659 cost $5.48 (5.1x median): its first opus run hit the 80-turn cap.

## Evidence

Location: `logs/BL-3659-20261026-151632-L4.jsonl`

Run L4 on 2026-10-26, 18:15:17 to 18:52:39: 81 turns in 37.3 min, requeued on the 80-turn cap (DarkFactory-20261026-151632-L4.log:148). The retry on L6, 2026-10-28 06:07:53 to 06:35:54, took 46 turns and finished Done. The cap was the driver.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-3659'
```

- Expected: No task over 3x the median costUsd and over 5 USD.
- Actual: BL-3659 costUsd 5.4816, claims 2, outcome done.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
- 2026-10-31: accepted -> closed. Audit 2026-10-31_0358.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-4237) are Done at bfe08061f844, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
