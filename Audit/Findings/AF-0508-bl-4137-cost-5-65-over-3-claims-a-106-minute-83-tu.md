---
id: AF-0508
title: BL-4137 cost $5.65 over 3 claims; a 106-minute, 83-turn run ($3.91) ran out of time with its code stashed
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-4137:BL-4137:cost-outlier
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
# AF-0508 - BL-4137 cost $5.65 over 3 claims; a 106-minute, 83-turn run ($3.91) ran out of time with its code stashed

## Summary

Low finding from the process auditor at `logs/BL-4137-20261030-060635-L6.jsonl`: BL-4137 cost $5.65 over 3 claims; a 106-minute, 83-turn run ($3.91) ran out of time with its code stashed.

## Evidence

Location: `logs/BL-4137-20261030-060635-L6.jsonl`

Runs: BL-4137-20261029-184521-L5.jsonl (21 turns, $0.75; split for size); BL-4137-20261030-060635-L6.jsonl (83 turns, $3.91, 106 min, 8.2 million cache-read tokens; result 'I ran out of time ... uncommitted, for the shift to stash'); BL-4137-20261030-060635-L4.jsonl (35 turns, $0.99; Done). The long run that could not commit drove the cost.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-4137'
```

- Expected: costUsd under $5 or under 3 times the median.
- Actual: BL-4137 claims 3, costUsd 5.6532, outcome done

## Re-audits

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
