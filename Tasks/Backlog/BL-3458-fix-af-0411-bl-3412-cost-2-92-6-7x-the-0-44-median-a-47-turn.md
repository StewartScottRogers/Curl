---
id: BL-3458
title: Fix AF-0411: BL-3412 cost $2.92 (6.7x the $0.44 median): a 47-turn Opus retry that ran out of cap after an earlier Opus run found an undeclared dependency
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-24
completed:
---
# BL-3458 — Fix AF-0411: BL-3412 cost $2.92 (6.7x the $0.44 median): a 47-turn Opus retry that ran out of cap after an earlier Opus run found an undeclared dependency

## Goal

What drove the past run's cost that the audit office reported as AF-0411 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0411 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0411-bl-3412-cost-2-92-6-7x-the-0-44-median-a-47-turn-o.md`.

Location: `logs/BL-3412-20261023-171352-L3.jsonl`

Location: `logs/BL-3412-20261023-171352-L3.jsonl`

Run BL-3412-20261023-110315-L4.jsonl: opus, 25 turns, 5.7 min, $0.73, ending in REQUEUE 'Depends on BL-3410'. Run BL-3412-20261023-171352-L3.jsonl: opus retry, 47 turns, 9.6 min, $2.19, 3.73M cache-read tokens; code done but cost cap reached before the gates, work stashed. A separate split run (split-BL-3412-20261023-191239-L1.jsonl, sonnet, $0.26) was not counted. Cost drivers: one wasted Opus run on a task that was not ready, and an Opus retry rereading a large context.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5
```

- Expected: no task over 3 times the median costUsd
- Actual: BL-3412 costUsd 2.9222, top of the list; median of the 177 task costs is 0.4392

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
