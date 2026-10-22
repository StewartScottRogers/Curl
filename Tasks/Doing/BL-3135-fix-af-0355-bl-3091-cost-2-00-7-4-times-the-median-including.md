---
id: BL-3135
title: Fix AF-0355: BL-3091 cost $2.00, 7.4 times the median, including a second run to resolve a rebase conflict
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3135 — Fix AF-0355: BL-3091 cost $2.00, 7.4 times the median, including a second run to resolve a rebase conflict

## Goal

What drove the past run's cost that the audit office reported as AF-0355 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0355 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0355-bl-3091-cost-2-00-7-4-times-the-median-including-a.md`.

Location: `logs/BL-3091-20261022-062551-L5.jsonl`

Location: `logs/BL-3091-20261022-062551-L5.jsonl`

The main run (L5, 06:57:59-07:08:46) took 36 turns, 10.8 min, cache_read 2,443,684, output 32,353, cost 1.7365. A second run, BL-3091-20261022-062551-L5-resolve.jsonl, took 13 turns, 4.5 min and cost 0.2660 (end 07:13:22), so a rebase conflict added about 15% to the cost. Task: split a task that hit its cost cap.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3091'
```

- Expected: costUsd at most 0.8106
- Actual: claims 1, minutes 17.88, costUsd 2.0025, done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
