---
id: AF-0507
title: BL-4342 cost $5.76: an 81-turn, 19-minute run hit the turn cap at $5.00 before a 19-turn rerun finished it
auditor: process
severity: Low
status: closed
reason: Its task BL-4417 is Done at work/dark-factory edc61f80642f; a cost-outlier finding records a past run's cost, which no fix can change, so it closes as task-done-historical (ADR-0422, 2026-10-14).
key: process:BL-4342:BL-4342:cost-outlier
reproduction: none
task: BL-4417
tasks: BL-4417
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed: 2026-10-31
closed-how: task-done-historical
closed-by: 2026-10-31_1149.md
---
# AF-0507 - BL-4342 cost $5.76: an 81-turn, 19-minute run hit the turn cap at $5.00 before a 19-turn rerun finished it

## Summary

Low finding from the process auditor at `logs/BL-4342-20261029-184521-L13.jsonl`: BL-4342 cost $5.76: an 81-turn, 19-minute run hit the turn cap at $5.00 before a 19-turn rerun finished it.

## Evidence

Location: `logs/BL-4342-20261029-184521-L13.jsonl`

BL-4342-20261029-184521-L13.jsonl took 81 turns, $5.00, 19 min and 10.3 million cache-read tokens. The lane trace shows 'end 81 turns, 18.9 min' at 01:29:29, followed at 01:30:25 by a line labelled DONE whose text is a claim ('Backlog -> Doing'). BL-4342-20261030-060635-L1.jsonl then finished in 19 turns and $0.76 (Done 06:36:59). 87% of the cost went into the capped run.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-4342'
```

- Expected: costUsd under $5 or under 3 times the median.
- Actual: BL-4342 claims 2, costUsd 5.7586, outcome done

## Re-audits

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | By mechanism over this window: no task over $5 (maximum $1.65) and no run hit the turn cap (maximum 40 turns). Fix task BL-4417 Done.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
- 2026-10-31: accepted -> closed. Its task BL-4417 is Done at work/dark-factory edc61f80642f; a cost-outlier finding records a past run's cost, which no fix can change, so it closes as task-done-historical (ADR-0422, 2026-10-14).
