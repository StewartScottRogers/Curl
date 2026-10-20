---
id: AF-0287
title: BL-2734 cost $3.96, 3.8 times the median task cost of $1.04 (71 turns, 49 Bash calls)
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-19_2134.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-2814) are Done at 1b3c70b1436b, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-2734:cost-outlier
reproduction: none
task: BL-2814
tasks: BL-2814
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed: 2026-10-19
closed-how: task-done-historical
closed-by: 2026-10-19_2134.md
---
# AF-0287 - BL-2734 cost $3.96, 3.8 times the median task cost of $1.04 (71 turns, 49 Bash calls)

## Summary

Low finding from the process auditor at `logs/BL-2734-20261018-103803-L1.jsonl`: BL-2734 cost $3.96, 3.8 times the median task cost of $1.04 (71 turns, 49 Bash calls).

## Evidence

Location: `logs/BL-2734-20261018-103803-L1.jsonl`

Measure-FactoryProcess.ps1 -Since 2026-10-18: BL-2734 claims=1 minutes=34.17 costUsd=3.9613 outcome=done; threshold 3.1176 (3 times the median 1.0392). The run result shows num_turns=71, 9318792 cache-read tokens, 32714 output tokens, 69 tool calls of which 49 were Bash, model opus. Lane log L1 runs from 12:34:41 claim ('curl_url_set keeps an empty host under CURLU_NO_AUTHORITY ...') to 13:08:51 DONE, with 3 builds, 4 tests, 3 quality runs and 11 edits. Most of the cost is cache reads across 71 turns: build, test and quality cycles, each re-reading the whole context.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261018-190531\logs -CiRunsJson Z:\repos\Curl.audit\20261018-190531\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2734'
```

- Expected: costUsd at most 3 times the median (3.1176).
- Actual: BL-2734 costUsd 3.9613.

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
- 2026-10-19: accepted -> closed. Audit 2026-10-19_2134.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-2814) are Done at 1b3c70b1436b, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
