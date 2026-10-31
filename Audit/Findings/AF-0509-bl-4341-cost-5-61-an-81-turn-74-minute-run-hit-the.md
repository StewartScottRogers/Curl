---
id: AF-0509
title: BL-4341 cost $5.61: an 81-turn, 74-minute run hit the turn cap at $4.93 before a 21-turn rerun finished it
auditor: process
severity: Low
status: proposed
reason:
key: process:BL-4341:BL-4341:cost-outlier
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
# AF-0509 - BL-4341 cost $5.61: an 81-turn, 74-minute run hit the turn cap at $4.93 before a 21-turn rerun finished it

## Summary

Low finding from the process auditor at `logs/BL-4341-20261029-184521-L1.jsonl`: BL-4341 cost $5.61: an 81-turn, 74-minute run hit the turn cap at $4.93 before a 21-turn rerun finished it.

## Evidence

Location: `logs/BL-4341-20261029-184521-L1.jsonl`

BL-4341-20261029-184521-L1.jsonl took 81 turns, $4.93, 74 min and 9.8 million cache-read tokens, and was requeued at 00:55:01 ('Dark factory stopped at its 80-turn cap'). BL-4341-20261029-184521-L13.jsonl then finished in 21 turns and $0.68 (Done 01:10:18). The pattern matches BL-4342 and BL-4112: the capped run's rereading drove the cost, and a short rerun did the rest.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-4341'
```

- Expected: costUsd under $5 or under 3 times the median.
- Actual: BL-4341 claims 2, costUsd 5.6059, outcome done

## Re-audits

## Log

- 2026-10-31: filed proposed.
