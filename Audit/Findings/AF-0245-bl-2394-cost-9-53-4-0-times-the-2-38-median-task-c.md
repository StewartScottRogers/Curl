---
id: AF-0245
title: BL-2394 cost $9.53, 4.0 times the $2.38 median task cost: three claims, a 90-turn final run and a conflict-resolve run
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-2394:BL-2394:cost-outlier
reproduction: none
task: BL-2582
tasks: BL-2582
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0245 - BL-2394 cost $9.53, 4.0 times the $2.38 median task cost: three claims, a 90-turn final run and a conflict-resolve run

## Summary

Low finding from the process auditor at `logs/BL-2394-20261016-081911-L2.jsonl`: BL-2394 cost $9.53, 4.0 times the $2.38 median task cost: three claims, a 90-turn final run and a conflict-resolve run.

## Evidence

Location: `logs/BL-2394-20261016-081911-L2.jsonl`

Median costUsd over 254 tasks = 2.3824; threshold 3x = 7.15; only BL-2394 (9.5261) exceeds it. The 5 costliest: BL-2394 $9.53 (3 claims), BL-2324 $6.78, BL-2495 $6.77, BL-2318 $6.29 (2 claims), BL-2443 $6.10 (2 claims). BL-2394 runs: 20261015-200601-L2 $1.47 / 19 turns (requeued, split), 20261016-043300-L2 $1.91 / 23 turns (requeued, waits on BL-2479), 20261016-081911-L2 $5.66 / 90 turns (done), 20261016-081911-L2-resolve $0.48 / 9 turns (rebase conflict). What drove it: $3.38 on two runs that ended in requeues, and a long 90-turn final run that also hit a denied 'git stash list' permission at its format step (DarkFactory-20261016-081911-L2.log:162).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-15 -LogRoot Z:\repos\Curl.audit\20261016-162347\logs -CiRunsJson Z:\repos\Curl.audit\20261016-162347\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5
```

- Expected: No task over 3 times the median costUsd (7.15).
- Actual: BL-2394 claims 3, minutes 24.6, costUsd 9.5261.

## Re-audits

- 2026-10-18 | 2026-10-18_1045.md | reproduces: yes | The original reproduction still shows BL-2394 at $9.5261 at the top of the cost list (next: BL-2324 $6.78). Re-audited by mechanism over 2026-10-16 onward (median $2.4326, 3x = $7.30): BL-2527 cost $7.9658 (3.27x) from a 94-turn, 34.1-min run plus a rebase-conflict resolve run (11 turns, $0.47), the same pattern as the long final run plus conflict-resolve run named here. BL-2550 at $7.3905 (3.04x) also crosses.

## Log

- 2026-10-16: filed proposed.
- 2026-10-16: proposed -> accepted.
