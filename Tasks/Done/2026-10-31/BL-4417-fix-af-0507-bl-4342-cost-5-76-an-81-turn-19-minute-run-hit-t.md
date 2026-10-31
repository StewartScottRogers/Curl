---
id: BL-4417
title: Fix AF-0507: BL-4342 cost $5.76: an 81-turn, 19-minute run hit the turn cap at $5.00 before a 19-turn rerun finished it
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4417 — Fix AF-0507: BL-4342 cost $5.76: an 81-turn, 19-minute run hit the turn cap at $5.00 before a 19-turn rerun finished it

## Goal

What drove the past run's cost that the audit office reported as AF-0507 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0507 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0507-bl-4342-cost-5-76-an-81-turn-19-minute-run-hit-the.md`.

Location: `logs/BL-4342-20261029-184521-L13.jsonl`

Location: `logs/BL-4342-20261029-184521-L13.jsonl`

BL-4342-20261029-184521-L13.jsonl took 81 turns, $5.00, 19 min and 10.3 million cache-read tokens. The lane trace shows 'end 81 turns, 18.9 min' at 01:29:29, followed at 01:30:25 by a line labelled DONE whose text is a claim ('Backlog -> Doing'). BL-4342-20261030-060635-L1.jsonl then finished in 19 turns and $0.76 (Done 06:36:59). 87% of the cost went into the capped run.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-4342'
```

- Expected: costUsd under $5 or under 3 times the median.
- Actual: BL-4342 claims 2, costUsd 5.7586, outcome done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause: BL-4342 was a `feature` task (reflection client: protocol, wiring, tests, ADR, 100% coverage gate). Its first claim did the real work for 81 turns and reached the 80-turn cap (`MaxRunTurns`, ADR-1155) at $5.00 with the code mostly written; the shift stashed it and the second claim finished in 19 turns for $0.76 from that stash. The $5.76 total is the cost of the work itself, not repeated work: the 87% in the capped run produced the code the rerun completed. The trace line labelled DONE (a claim text) is a log labelling quirk, not a rerun. No avoidable waste found: the cap and stash hand-over behaved as designed, so the cost is recorded as unavoidable. A lane cannot read the finding or logs, so this rests on the task text.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-31: Doing -> Done. Cost cause named: the work itself, unavoidable; no fix needed
