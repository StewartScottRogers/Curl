---
id: AF-0288
title: BL-2751 cost $3.90, 3.7 times the median task cost of $1.04 (87 turns, 27 edits)
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-2751:cost-outlier
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
# AF-0288 - BL-2751 cost $3.90, 3.7 times the median task cost of $1.04 (87 turns, 27 edits)

## Summary

Low finding from the process auditor at `logs/BL-2751-20261018-103803-L3.jsonl`: BL-2751 cost $3.90, 3.7 times the median task cost of $1.04 (87 turns, 27 edits).

## Evidence

Location: `logs/BL-2751-20261018-103803-L3.jsonl`

Measure-FactoryProcess.ps1 -Since 2026-10-18: BL-2751 claims=1 minutes=35.73 costUsd=3.8954 outcome=done; threshold 3.1176. The run result shows num_turns=87, 7411527 cache-read tokens, 57116 output tokens, 85 tool calls (41 Bash), model opus. Lane log DarkFactory-20261018-103803-L3.log runs from 13:21:03 claim ('RtspProtocolHandler makes any libcurl RTSP request from per-perform s...') to 13:56:47 DONE, with 27 edits, 5 writes, 4 quality runs and 2 renums. The task was the prerequisite BL-2704 was requeued to wait on at 13:20:45. The cost came from editing a broad handler surface (27 edits) and from 4 quality cycles.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261018-190531\logs -CiRunsJson Z:\repos\Curl.audit\20261018-190531\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2751'
```

- Expected: costUsd at most 3 times the median (3.1176).
- Actual: BL-2751 costUsd 3.8954.

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
