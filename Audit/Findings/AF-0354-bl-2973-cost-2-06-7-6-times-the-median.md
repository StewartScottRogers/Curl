---
id: AF-0354
title: BL-2973 cost $2.06, 7.6 times the median
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-22_1902.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3134) are Done at 9ada2459e768, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-2973:cost-outlier
reproduction: none
task: BL-3134
tasks: BL-3134
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed: 2026-10-22
closed-how: task-done-historical
closed-by: 2026-10-22_1902.md
---
# AF-0354 - BL-2973 cost $2.06, 7.6 times the median

## Summary

Low finding from the process auditor at `logs/BL-2973-20261022-024438-L2.jsonl`: BL-2973 cost $2.06, 7.6 times the median.

## Evidence

Location: `logs/BL-2973-20261022-024438-L2.jsonl`

One Opus feature run (L2, 02:45:19-03:00:29): 47 turns, 15.2 min, cache_read 3,450,517, output 25,466 tokens, cost 2.0600, writing curl 8.22.0's DoH sub-transfer trace lines. The cost comes from turns and cache rereads; it was claimed once.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2973'
```

- Expected: costUsd at most 0.8106
- Actual: claims 1, minutes 23.02, costUsd 2.06, done

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-22: accepted -> closed. Audit 2026-10-22_1902.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3134) are Done at 9ada2459e768, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
