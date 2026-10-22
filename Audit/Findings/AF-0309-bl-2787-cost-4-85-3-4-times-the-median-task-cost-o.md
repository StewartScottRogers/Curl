---
id: AF-0309
title: BL-2787 cost $4.85, 3.4 times the median task cost of $1.42
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-22_1120.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-2947) are Done at 608a75558667, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-2787:cost-outlier
reproduction: none
task: BL-2947
tasks: BL-2947
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed: 2026-10-22
closed-how: task-done-historical
closed-by: 2026-10-22_1120.md
---
# AF-0309 - BL-2787 cost $4.85, 3.4 times the median task cost of $1.42

## Summary

Low finding from the process auditor at `logs/BL-2787-20261018-192454-L1.jsonl`: BL-2787 cost $4.85, 3.4 times the median task cost of $1.42.

## Evidence

Location: `logs/BL-2787-20261018-192454-L1.jsonl`

Median costUsd over the 100 costed tasks is 1.4155, so the 3x bar is 4.2465. The 5 costliest tasks: BL-2787 4.8482, BL-2743 4.7472, BL-2788 4.6076, BL-2724 4.4497, BL-2726 4.1982. BL-2787 was one run: 82 turns in 24.6 min (claim 03:33:38, end 03:58:18 in DarkFactory-20261018-192454-L1.log), 10.83M cache-read input tokens (about 132K context re-read per turn), 47K output. Tools: 49 Bash calls (9 of them dotnet build, 1 dotnet test), 27 Edit, 2 Read. What drove the cost: a long single session that rebuilt 9 times while the whole context was re-read every turn. No retries.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5
```

- Expected: No task over 3 times the median costUsd (4.2465).
- Actual: BL-2787 4.8482, BL-2743 4.7472, BL-2788 4.6076, BL-2724 4.4497, BL-2726 4.1982

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: yes | The original reproduction still lists BL-2787 at 4.8482 as the top cost. By mechanism, this window has 5 tasks over 3 times the median cost of $1.8006: BL-2845 7.4985, BL-2938 5.7798, BL-2970 5.602, BL-2924 5.5624, BL-2859 5.4413. BL-2924's single 83-turn run cost $5.06, the same single-long-run cause as BL-2787.

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
- 2026-10-22: accepted -> closed. Audit 2026-10-22_1120.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-2947) are Done at 608a75558667, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
