---
id: BL-3089
title: Fix AF-0336: BL-2859 cost $5.44, 3.0 times the median: a first run requeued on a dependency, then a 79-turn second run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3089 — Fix AF-0336: BL-2859 cost $5.44, 3.0 times the median: a first run requeued on a dependency, then a 79-turn second run

## Goal

What drove the past run's cost that the audit office reported as AF-0336 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0336 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0336-bl-2859-cost-5-44-3-0-times-the-median-a-first-run.md`.

Location: `logs/DarkFactory-20261021-140711-L5.log:54`

Location: `logs/DarkFactory-20261021-140711-L5.log:54`

BL-2859-20261021-064209-L5: 23 turns, $1.01. Then REQUEUE 10:16:29 'Waits on BL-2955: Curl cannot reach a S...' (socks5unix). BL-2859-20261021-140711-L5: 79 turns, 29.5 min, $4.44, 9.5M cache-read tokens, 62 dotnet build/test mentions. DONE 14:54:07. The cost comes from a dependency found only after the claim, and from a long build-and-test loop for two server stand-ins.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2859'
```

- Expected: costUsd at most 3 times the median (5.40).
- Actual: claims=2, costUsd=5.4413, outcome=done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
