---
id: BL-3133
title: Fix AF-0353: BL-3028 cost $2.44, 9 times the median
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3133 — Fix AF-0353: BL-3028 cost $2.44, 9 times the median

## Goal

What drove the past run's cost that the audit office reported as AF-0353 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0353 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0353-bl-3028-cost-2-44-9-times-the-median.md`.

Location: `logs/BL-3028-20261022-014530-L1.jsonl`

Location: `logs/BL-3028-20261022-014530-L1.jsonl`

One Opus run (L1, 01:46:01-01:59:16): 60 turns, 13.2 min, cache_read 3,389,432, output 40,972 tokens, cost 2.4371. Task: make the CI watch file job-step failures with real touches (the BL-3026 empty-touches fix). The cost comes from turn count and cache rereads; there were no retries.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3028'
```

- Expected: costUsd at most 0.8106
- Actual: claims 1, minutes 14.17, costUsd 2.4371, done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
