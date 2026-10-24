---
id: AF-0389
title: BL-3271 measured at $4.65 (4.4x the median) because two different tasks shared its ID; the merged cost then tripped the cost cap and a split that failed
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-24_0845.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3366) are Done at d3ee244c64c5, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-3271:cost-outlier
reproduction: none
task: BL-3366
tasks: BL-3366
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed: 2026-10-24
closed-how: task-done-historical
closed-by: 2026-10-24_0845.md
---
# AF-0389 - BL-3271 measured at $4.65 (4.4x the median) because two different tasks shared its ID; the merged cost then tripped the cost cap and a split that failed

## Summary

Low finding from the process auditor at `logs/BL-3271-20261023-035126-L2.jsonl`: BL-3271 measured at $4.65 (4.4x the median) because two different tasks shared its ID; the merged cost then tripped the cost cap and a split that failed.

## Evidence

Location: `logs/BL-3271-20261023-035126-L2.jsonl`

Done-task median cost is $1.062, so the threshold is $3.19. The tool's $4.65 for BL-3271 adds up runs from two tasks with the same ID. L2's 'Land per-handle caches' (split from BL-3204) ran Opus for 44 turns, 25.4 min, $2.91. L3's 'Keep -Z URL order when the host's first connection stays alive' ran Sonnet for 86 turns, $1.59, plus a $0.15 resolve run. Both lanes logged a DONE for BL-3271 (L2 04:21:49, L3 04:30:54). At 04:19:42 L4 found BL-3271 'unclaimable cost cap: its runs of the last day already cost 3.69 of its 3.82'. That cap was computed on the merged cost. L4 renumbered 'BL-3271 -> BL-3276' at 04:20:12 and spent a split run ($0.18), whose push was refused ('split failed: the push of the split was refused; blocked', 04:22:53). The avoidable waste is the duplicate ID, not either run: neither task's own cost exceeds the threshold.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261023-035126-L*.log -Pattern 'BL-3271 (claim|DONE|unclaimable|split)|renum'
```

- Expected: No task over 3x the median cost; one task per ID.
- Actual: tasks[BL-3271].costUsd = 4.6482; claims of two different titles under BL-3271 by L2 and L3.

## Re-audits

- 2026-10-23 | 2026-10-23_2322.md | reproduces: yes | Same cause, cost inflated by two different tasks sharing one ID: BL-3314 measures $1.44 (3.3x the $0.44 median, over the 3x line). That is the sum of BL-3314-20261023-035126-L4.jsonl (CURLINFO_PRIMARY_PORT, sonnet, 45 turns, $0.95) and BL-3314-20261023-035126-L1.jsonl (an unrelated Gource-render task under the same ID, sonnet, 40 turns, $0.49). This time it tripped no cost cap or split.

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> accepted.
- 2026-10-24: accepted -> closed. Audit 2026-10-24_0845.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3366) are Done at d3ee244c64c5, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
