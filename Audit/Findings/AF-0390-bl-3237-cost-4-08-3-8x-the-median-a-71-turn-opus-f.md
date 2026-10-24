---
id: AF-0390
title: BL-3237 cost $4.08 (3.8x the median): a 71-turn Opus feature run plus two rebase-conflict resolve runs
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-23_2322.md: the reproduction no longer reproduces.
key: process:logs:BL-3237:cost-outlier
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
# AF-0390 - BL-3237 cost $4.08 (3.8x the median): a 71-turn Opus feature run plus two rebase-conflict resolve runs

## Summary

Low finding from the process auditor at `logs/BL-3237-20261023-035126-L1.jsonl`: BL-3237 cost $4.08 (3.8x the median): a 71-turn Opus feature run plus two rebase-conflict resolve runs.

## Evidence

Location: `logs/BL-3237-20261023-035126-L1.jsonl`

Main run: Opus, 71 turns, 29.7 min, $3.64 (04:39:56 to 05:09:43). Two resolve runs: 12 turns $0.31 and 17 turns $0.44. Each resolve run reran the whole fast suite (19221/19221 and 20742/20742 pass). The lane trace shows two failed test turns: 'test FAIL grep: C:\Users\Stewart Rogers\AppData\Local\Temp/...' at 04:50:09, and 'test FAIL Permission to use Bash with command cd /z/repos/C...' at 05:08:56, where a disallowed shell command cost a retry.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261023-035126-L1.log -Pattern 'BL-3237 (claim|test|end|resolve)'
```

- Expected: costUsd at most 3x the median ($3.19).
- Actual: tasks[BL-3237].costUsd = 4.0802

## Re-audits

- 2026-10-23 | 2026-10-23_2322.md | reproduces: no | Mechanism over this window: no task over 3x the median is driven by rebase-conflict resolve runs. The window's resolve runs (BL-3352, BL-3395, BL-3396) were sonnet, 7-10 turns, $0.10-$0.12 each, and none of those tasks is an outlier (BL-3395 $1.28 and BL-3396 $1.15, both under 3x = $1.32). BL-3237's resolve lines at 05:09/05:16 are the original incident, and its runs are not in this log copy (costUsd 0).

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> closed. Re-audit 2026-10-23_2322.md: the reproduction no longer reproduces.
