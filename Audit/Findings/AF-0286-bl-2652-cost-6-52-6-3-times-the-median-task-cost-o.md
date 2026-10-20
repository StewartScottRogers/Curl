---
id: AF-0286
title: BL-2652 cost $6.52, 6.3 times the median task cost of $1.04 (lib1560 port, 110 turns)
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-19_2134.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-2813) are Done at 1b3c70b1436b, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-2652:cost-outlier
reproduction: none
task: BL-2813
tasks: BL-2813
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed: 2026-10-19
closed-how: task-done-historical
closed-by: 2026-10-19_2134.md
---
# AF-0286 - BL-2652 cost $6.52, 6.3 times the median task cost of $1.04 (lib1560 port, 110 turns)

## Summary

Low finding from the process auditor at `logs/BL-2652-20261018-103803-L1.jsonl`: BL-2652 cost $6.52, 6.3 times the median task cost of $1.04 (lib1560 port, 110 turns).

## Evidence

Location: `logs/BL-2652-20261018-103803-L1.jsonl`

Measure-FactoryProcess.ps1 -Since 2026-10-18: BL-2652 claims=1 minutes=31.5 costUsd=6.5191 outcome=done; median costUsd over 89 claimed tasks is 1.0392 (threshold 3.1176); median over the 71 done tasks is 1.0498. The run result shows num_turns=110, 99035 output tokens, 13287036 cache-read tokens, 235041 cache-creation tokens, 109 tool calls (22 Read, 48 Bash), model opus (feature pipeline). Lane log DarkFactory-20261018-103803-L1.log has 11:09:58 claim 'Port libtest program 1560 (the URL API test, 2549 lines ...', 25 edit and 8 write lines, and test passes climbing 26/29 -> 27/29 -> 1163/1164 -> ok, then a final 'test 0/1 pass' at 11:39:24 before DONE at 11:41:28. The cost came from the size of the port (99k output tokens) and from fix-and-retest cycles with the whole context re-read each turn. Ranked by cost, this is the most expensive of the 5 costliest tasks.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261018-190531\logs -CiRunsJson Z:\repos\Curl.audit\20261018-190531\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5; Select-String -Path Z:\repos\Curl.audit\20261018-190531\logs\DarkFactory-20261018-103803-L1.log -Pattern 'BL-2652 (claim|test|DONE)'
```

- Expected: No task's costUsd is over 3 times the median costUsd.
- Actual: BL-2652 costUsd 6.5191 against a median of 1.0392 (6.3 times).

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
- 2026-10-19: accepted -> closed. Audit 2026-10-19_2134.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-2813) are Done at 1b3c70b1436b, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
