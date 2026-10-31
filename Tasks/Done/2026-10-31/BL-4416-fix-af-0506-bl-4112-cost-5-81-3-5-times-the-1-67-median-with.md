---
id: BL-4416
title: Fix AF-0506: BL-4112 cost $5.81 (3.5 times the $1.67 median) with no Done; one 81-turn run hit the turn cap at $5.10
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4416 — Fix AF-0506: BL-4112 cost $5.81 (3.5 times the $1.67 median) with no Done; one 81-turn run hit the turn cap at $5.10

## Goal

What drove the past run's cost that the audit office reported as AF-0506 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0506 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0506-bl-4112-cost-5-81-3-5-times-the-1-67-median-with-n.md`.

Location: `logs/BL-4112-20261029-140509-L5.jsonl`

Location: `logs/BL-4112-20261029-140509-L5.jsonl`

Runs: BL-4112-20261029-085044-L3.jsonl (20 turns, $0.70); BL-4112-20261029-140509-L5.jsonl (81 turns, $5.10, 55 min, 10.2 million cache-read tokens); split-BL-4112-20261029-164929-L7.jsonl (14 turns, $0.21). The cost came from rereading a growing context for 81 turns, then stopping at the 80-turn cap with nothing integrated. Afterwards the task was split and Deferred. The 5 costliest tasks: BL-4112 $5.81, BL-4342 $5.76, BL-4137 $5.65, BL-4341 $5.61, BL-4131 $4.93.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | sort costUsd -desc | select -first 5
```

- Expected: No task over 3 times the median cost and over $5.
- Actual: BL-4112 claims 2, costUsd 5.8051, outcome requeued

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause: BL-4112 was too big for one run. Its 81-turn run (L5) reread a growing context (10.2M cache-read tokens) until the 80-turn cap (`MaxRunTurns`, RunDarkFactory.ps1:502) stopped it at $5.10. The cap then did its job: the split run (L7, $0.21) filed smaller tasks and deferred BL-4112 (ADR-1155). Remaining waste was the 60+ turns before the size was noticed; the lane prompt already tells a run to hand back by turn 65 (AF-0482) and to size the task before the first edit, which post-dates this run. No further change to RunDarkFactory.ps1: the cost was the intended cap behaviour, recorded as unavoidable. No code changed, so build and tests are unaffected.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-31: Doing -> Done. Cause named: BL-4112 too big for one run, 80-turn cap worked as designed; recorded as unavoidable
