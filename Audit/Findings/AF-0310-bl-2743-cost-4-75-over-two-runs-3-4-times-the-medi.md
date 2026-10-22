---
id: AF-0310
title: BL-2743 cost $4.75 over two runs, 3.4 times the median, after a requeue that stashed finished engine work
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-22_1120.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-2948) are Done at 608a75558667, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-2743:cost-outlier
reproduction: none
task: BL-2948
tasks: BL-2948
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed: 2026-10-22
closed-how: task-done-historical
closed-by: 2026-10-22_1120.md
---
# AF-0310 - BL-2743 cost $4.75 over two runs, 3.4 times the median, after a requeue that stashed finished engine work

## Summary

Low finding from the process auditor at `logs/BL-2743-20261018-192454-L1.jsonl`: BL-2743 cost $4.75 over two runs, 3.4 times the median, after a requeue that stashed finished engine work.

## Evidence

Location: `logs/BL-2743-20261018-192454-L1.jsonl`

Run 1 (BL-2743-20261018-192454-L1.jsonl): $2.96, 41 turns, 10.9 min, 5 builds, then requeued at 21:52:02 with 'Engine fix done and stashed; left: ...' (stash 3b7b8a47). Run 2 (BL-2743-20261019-061607-L3.jsonl): $1.78, 26 turns, 19.6 min. It applied stash 3b7b8a47 and re-ran the full build and test (33813/33813, 3573/3573). It got 'quality FAIL' at 06:29:15 and ran quality 3 more times before DONE at 06:38:11. What drove the cost: about 38% of it came from the second session rebuilding context and re-verifying stashed work. Stash 3b7b8a47 is still in git stash list after being applied.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2743'
```

- Expected: costUsd at most 4.2465 (3 times the median).
- Actual: BL-2743 claims 2, minutes 21.18, costUsd 4.7472, done

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: yes | The original reproduction still gives BL-2743 claims=2, costUsd=4.7472. The same cause recurs in this window: a requeue that stashed finished work, then a costly second run. BL-2938: first run 60 turns $3.12, work stashed and requeued; stash d6503b8f 'did not apply cleanly' at 14:07:43; second run $2.20 plus resolve $0.46; total $5.78, 3.2 times the median.

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
- 2026-10-22: accepted -> closed. Audit 2026-10-22_1120.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-2948) are Done at 608a75558667, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
