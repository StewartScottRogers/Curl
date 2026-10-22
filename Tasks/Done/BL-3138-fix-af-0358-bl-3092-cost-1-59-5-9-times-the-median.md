---
id: BL-3138
title: Fix AF-0358: BL-3092 cost $1.59, 5.9 times the median
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3138 — Fix AF-0358: BL-3092 cost $1.59, 5.9 times the median

## Goal

What drove the past run's cost that the audit office reported as AF-0358 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0358 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0358-bl-3092-cost-1-59-5-9-times-the-median.md`.

Location: `logs/BL-3092-20261022-062551-L7.jsonl`

Location: `logs/BL-3092-20261022-062551-L7.jsonl`

One run: 46 turns, 7.6 min, cache_read 2,767,634, output 23,993, cost 1.5878. It was claimed once.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3092'
```

- Expected: costUsd at most 0.8106
- Actual: claims 1, minutes 8.92, costUsd 1.5878, done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-22, lane 2. Cause: BL-3092 changed four DarkFactory PowerShell files (idle poll, heartbeat, hand-over scriptblock, self-test) on one claim, no requeue, no rework: 46 turns each re-reading a growing context, so cache_read of 2.77M tokens is the cost; output was only 24k. It was done directly (no subagent stages) and landed Done with every gate green. Unavoidable: cost scales with turns on a multi-file change with many -Test switches to run; no defect in RunDarkFactory.ps1 is implicated, so no change is made. The run log is not in this checkout, so this rests on the task Notes and the finding figures.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-22: Doing -> Done. AF-0358 cost is cache rereads of one 46-turn multi-file change; unavoidable, no fix needed
