---
id: BL-3086
title: Fix AF-0333: BL-2938 cost $5.78, 3.2 times the median: a 60-turn first run whose stash did not apply on the second claim
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3086 — Fix AF-0333: BL-2938 cost $5.78, 3.2 times the median: a 60-turn first run whose stash did not apply on the second claim

## Goal

What drove the past run's cost that the audit office reported as AF-0333 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0333 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0333-bl-2938-cost-5-78-3-2-times-the-median-a-60-turn-f.md`.

Location: `logs/DarkFactory-20261021-140711-L1.log:2`

Location: `logs/DarkFactory-20261021-140711-L1.log:2`

BL-2938-20261021-064209-L3: 60 turns, $3.12. Its work was stashed, then REQUEUE 10:17:17 'Waits on BL-2956'. BL-2938-20261021-140711-L1: at 14:07:43, 'stash d6503b8f from an earlier run did not apply cleanly'. That run took 47 turns ($2.20), plus a resolve run of 9 turns ($0.46), DONE 14:43:48. More than half the cost was the first run, whose stash could not be applied directly.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2938'
```

- Expected: costUsd at most 3 times the median (5.40).
- Actual: claims=2, costUsd=5.7798, outcome=done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
