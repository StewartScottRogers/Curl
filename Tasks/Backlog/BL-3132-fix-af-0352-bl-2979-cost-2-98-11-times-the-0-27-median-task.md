---
id: BL-3132
title: Fix AF-0352: BL-2979 cost $2.98, 11 times the $0.27 median task cost
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3132 — Fix AF-0352: BL-2979 cost $2.98, 11 times the $0.27 median task cost

## Goal

What drove the past run's cost that the audit office reported as AF-0352 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0352 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0352-bl-2979-cost-2-98-11-times-the-0-27-median-task-co.md`.

Location: `logs/BL-2979-20261022-024438-L6.jsonl`

Location: `logs/BL-2979-20261022-024438-L6.jsonl`

One Opus feature-pipeline run (claimed 03:09:45 on L6, ended 03:32:11): 63 turns, 22.4 min, cache_read 5,606,826, cache_creation 154,871, output 30,945 tokens, cost 2.9796. This is the costliest task of the window. The cost comes from turns and rereading the cached context (5.6M cache-read tokens), not from retries: it was claimed once.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2979'
```

- Expected: costUsd at most 3 times the median (0.8106)
- Actual: claims 1, minutes 23.78, costUsd 2.9796, done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
