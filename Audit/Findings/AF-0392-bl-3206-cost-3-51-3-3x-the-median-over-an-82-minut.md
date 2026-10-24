---
id: AF-0392
title: BL-3206 cost $3.51 (3.3x the median) over an 82-minute, 65-turn Opus run
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-24_0845.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3369) are Done at d3ee244c64c5, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-3206:cost-outlier
reproduction: none
task: BL-3369
tasks: BL-3369
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed: 2026-10-24
closed-how: task-done-historical
closed-by: 2026-10-24_0845.md
---
# AF-0392 - BL-3206 cost $3.51 (3.3x the median) over an 82-minute, 65-turn Opus run

## Summary

Low finding from the process auditor at `logs/BL-3206-20261022-192710-L8.jsonl`: BL-3206 cost $3.51 (3.3x the median) over an 82-minute, 65-turn Opus run.

## Evidence

Location: `logs/BL-3206-20261022-192710-L8.jsonl`

Main run: Opus, 65 turns, 82.1 min, $3.27 (22:49:16 to 00:11:23). The first test line appears only at 00:00:50, 71 minutes after the claim. Resolve run: 11 turns, $0.25. The task (block in curl_multi_poll/curl_multi_wait on a handle's timeout) holds the lane far longer than the 16.7 minute median. The long untested stretch points to extended reading and design before the first build.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261022-192710-L8.log -Pattern 'BL-3206 (claim|test|end|resolve)'
```

- Expected: costUsd at most 3x the median ($3.19).
- Actual: tasks[BL-3206].costUsd = 3.5135, minutes 91.62

## Re-audits

- 2026-10-23 | 2026-10-23_2322.md | reproduces: yes | Same cause, one long Opus run alone driving cost past 3x the median: BL-3310, one opus run of 60 turns and 41.2 min, $2.68 (6.1x); BL-3418, one opus run of 49 turns, $2.90 (6.6x); BL-3416, one opus run of 41 turns, $2.69 (6.1x).

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> accepted.
- 2026-10-24: accepted -> closed. Audit 2026-10-24_0845.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3369) are Done at d3ee244c64c5, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
