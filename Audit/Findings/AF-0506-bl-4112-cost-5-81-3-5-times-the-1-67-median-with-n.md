---
id: AF-0506
title: BL-4112 cost $5.81 (3.5 times the $1.67 median) with no Done; one 81-turn run hit the turn cap at $5.10
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-4112:BL-4112:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0506 - BL-4112 cost $5.81 (3.5 times the $1.67 median) with no Done; one 81-turn run hit the turn cap at $5.10

## Summary

Low finding from the process auditor at `logs/BL-4112-20261029-140509-L5.jsonl`: BL-4112 cost $5.81 (3.5 times the $1.67 median) with no Done; one 81-turn run hit the turn cap at $5.10.

## Evidence

Location: `logs/BL-4112-20261029-140509-L5.jsonl`

Runs: BL-4112-20261029-085044-L3.jsonl (20 turns, $0.70); BL-4112-20261029-140509-L5.jsonl (81 turns, $5.10, 55 min, 10.2 million cache-read tokens); split-BL-4112-20261029-164929-L7.jsonl (14 turns, $0.21). The cost came from rereading a growing context for 81 turns, then stopping at the 80-turn cap with nothing integrated. Afterwards the task was split and Deferred. The 5 costliest tasks: BL-4112 $5.81, BL-4342 $5.76, BL-4137 $5.65, BL-4341 $5.61, BL-4131 $4.93.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | sort costUsd -desc | select -first 5
```

- Expected: No task over 3 times the median cost and over $5.
- Actual: BL-4112 claims 2, costUsd 5.8051, outcome requeued

## Re-audits

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
