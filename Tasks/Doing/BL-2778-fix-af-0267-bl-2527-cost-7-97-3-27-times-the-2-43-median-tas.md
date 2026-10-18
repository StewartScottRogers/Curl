---
id: BL-2778
title: Fix AF-0267: BL-2527 cost $7.97, 3.27 times the $2.43 median task cost: a 94-turn, 34-minute run plus a rebase-conflict resolve run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-18
completed:
---
# BL-2778 — Fix AF-0267: BL-2527 cost $7.97, 3.27 times the $2.43 median task cost: a 94-turn, 34-minute run plus a rebase-conflict resolve run

## Goal

What drove the past run's cost that the audit office reported as AF-0267 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0267 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0267-bl-2527-cost-7-97-3-27-times-the-2-43-median-task.md`.

Location: `logs/DarkFactory-20261017-021554-L5.log:49`

Location: `logs/DarkFactory-20261017-021554-L5.log:49`

Measure-FactoryProcess.ps1: BL-2527 claims 1, 39.27 min, $7.9658; median costUsd over 137 tasks is $2.4326 (3x = $7.30). Run BL-2527-20261017-021554-L5.jsonl: 94 turns, 2,045,880 ms, $7.49 (60 Bash, 21 Write, 10 Edit) to port libtest programs 500 to 509. Then BL-2527-20261017-021554-L5-resolve.jsonl: 'rebase conflict; resolving' at 02:53:50 (line 51), 11 turns, $0.47. Cost came from the task's size (ten programs in one run) and a 9-lane shift contending for the shared ratchet file. The other costliest tasks in the window are BL-2550 $7.39, BL-2549 $6.96, BL-2535 $6.53 and BL-2536 $6.44, all libtest port batches.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5
```

- Expected: No task over 3 times the median costUsd ($7.30).
- Actual: BL-2527 1 39.27 7.9658 done (top of the list).

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
