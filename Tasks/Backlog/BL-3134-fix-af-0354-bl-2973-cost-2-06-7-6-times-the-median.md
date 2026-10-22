---
id: BL-3134
title: Fix AF-0354: BL-2973 cost $2.06, 7.6 times the median
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3134 — Fix AF-0354: BL-2973 cost $2.06, 7.6 times the median

## Goal

What drove the past run's cost that the audit office reported as AF-0354 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0354 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0354-bl-2973-cost-2-06-7-6-times-the-median.md`.

Location: `logs/BL-2973-20261022-024438-L2.jsonl`

Location: `logs/BL-2973-20261022-024438-L2.jsonl`

One Opus feature run (L2, 02:45:19-03:00:29): 47 turns, 15.2 min, cache_read 3,450,517, output 25,466 tokens, cost 2.0600, writing curl 8.22.0's DoH sub-transfer trace lines. The cost comes from turns and cache rereads; it was claimed once.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2973'
```

- Expected: costUsd at most 0.8106
- Actual: claims 1, minutes 23.02, costUsd 2.06, done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
