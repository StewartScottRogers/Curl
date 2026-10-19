---
id: AF-0290
title: BL-2747 cost $3.21, 3.1 times the median task cost of $1.04 (63 turns, 6 test runs)
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-2747:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0290 - BL-2747 cost $3.21, 3.1 times the median task cost of $1.04 (63 turns, 6 test runs)

## Summary

Low finding from the process auditor at `logs/BL-2747-20261018-103803-L1.jsonl`: BL-2747 cost $3.21, 3.1 times the median task cost of $1.04 (63 turns, 6 test runs).

## Evidence

Location: `logs/BL-2747-20261018-103803-L1.jsonl`

Measure-FactoryProcess.ps1 -Since 2026-10-18: BL-2747 claims=1 minutes=24.67 costUsd=3.2101 outcome=done; threshold 3.1176 (it is also over 3 times the done-only median, 3.1494). The run result shows num_turns=63, 6474561 cache-read tokens, 30845 output tokens, 62 tool calls (37 Bash), model opus. Lane log L1 runs from 13:59:58 claim ('Keep a Libcurl handle's last Digest challenge ...') to 14:24:38 DONE, with 3 builds, 6 tests, 3 quality runs and 16 edits. Three other tasks waited on it: BL-2691 (13:10:25) and BL-2744 (13:24:49) were requeued to wait on it. The cost is test and quality cycles that re-read the whole context each turn.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261018-190531\logs -CiRunsJson Z:\repos\Curl.audit\20261018-190531\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2747'
```

- Expected: costUsd at most 3 times the median (3.1176).
- Actual: BL-2747 costUsd 3.2101.

## Re-audits

## Log

- 2026-10-18: filed proposed.
