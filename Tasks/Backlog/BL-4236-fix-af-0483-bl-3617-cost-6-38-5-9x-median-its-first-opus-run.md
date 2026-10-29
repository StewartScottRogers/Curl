---
id: BL-4236
title: Fix AF-0483: BL-3617 cost $6.38 (5.9x median): its first opus run hit the 80-turn cap, the second took 68 turns
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-29
completed:
---
# BL-4236 — Fix AF-0483: BL-3617 cost $6.38 (5.9x median): its first opus run hit the 80-turn cap, the second took 68 turns

## Goal

What drove the past run's cost that the audit office reported as AF-0483 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0483 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0483-bl-3617-cost-6-38-5-9x-median-its-first-opus-run-h.md`.

Location: `logs/BL-3617-20261026-063535-L5.jsonl`

Location: `logs/BL-3617-20261026-063535-L5.jsonl`

Run L5 on 2026-10-26, 13:03:08 to 13:32:11: 81 turns in 29 min, requeued at 13:36:17 on the 80-turn cap (DarkFactory-20261026-063535-L5.log:308). The retry on L1, 13:40:44 to 14:41:17, took 68 turns and 60.5 min and finished Done. The capped run's work was redone by the retry.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-3617'
```

- Expected: No task over 3x the median costUsd and over 5 USD.
- Actual: BL-3617 costUsd 6.3804, claims 2, outcome done.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
