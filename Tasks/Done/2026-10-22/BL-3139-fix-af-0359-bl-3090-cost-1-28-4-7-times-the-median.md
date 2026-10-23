---
id: BL-3139
title: Fix AF-0359: BL-3090 cost $1.28, 4.7 times the median
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3139 — Fix AF-0359: BL-3090 cost $1.28, 4.7 times the median

## Goal

What drove the past run's cost that the audit office reported as AF-0359 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0359 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0359-bl-3090-cost-1-28-4-7-times-the-median.md`.

Location: `logs/BL-3090-20261022-062551-L5.jsonl`

Location: `logs/BL-3090-20261022-062551-L5.jsonl`

One run: 32 turns, 9.4 min, cache_read 1,619,473, output 21,976, cost 1.2806. It was claimed once.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3090'
```

- Expected: costUsd at most 0.8106
- Actual: claims 1, minutes 15.03, costUsd 1.2806, done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause (AF-0359): the run log `logs/BL-3090-20261022-062551-L5.jsonl` is not in any lane checkout, so cost was read from the figures here and from BL-3090 itself. BL-3090 was a `feature` task that changed six DarkFactory scripts (TaskIds, TaskBudget, Claim, AutoLanesState, ShiftCoordinator, AutoLanes), added self-test cases to 3 switches, an ADR and 8 criteria, and re-ran 23 `-Test*` switches: 32 turns, 1.62M cache-read tokens (about 50k re-read per turn), 22k output tokens. Cost scales with turns times accumulated context, and one claim finished it with no requeue or rework. Its median ratio (4.7x) comes from the median being small tasks. Recorded as unavoidable; no change to RunDarkFactory.ps1 is warranted (same finding shape as AF-0358, BL-3138).

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-22: Doing -> Done. Cost cause named: BL-3090 was a 32-turn six-script feature change; unavoidable, no fix needed
