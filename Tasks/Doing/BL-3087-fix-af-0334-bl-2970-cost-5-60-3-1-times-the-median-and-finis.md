---
id: BL-3087
title: Fix AF-0334: BL-2970 cost $5.60, 3.1 times the median, and finished nothing itself: it was deferred and split
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Claim.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3087 — Fix AF-0334: BL-2970 cost $5.60, 3.1 times the median, and finished nothing itself: it was deferred and split

## Goal

What drove the past run's cost that the audit office reported as AF-0334 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0334 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0334-bl-2970-cost-5-60-3-1-times-the-median-and-finishe.md`.

Location: `logs/DarkFactory-20261021-064209-L1.log:311`

Location: `logs/DarkFactory-20261021-064209-L1.log:311`

BL-2970-20261021-064209-L1: 72 turns, $4.27, 7.4M cache-read tokens. It ended in a requeue for a file outside its touches, with the work stashed. BL-2970-20261021-140711-L8: 13 turns, $0.87; its stash did not apply cleanly. BL-2970-20261021-150011-L2: 5 turns, $0.46. Both later runs ended in cost-cap requeues. The work was carried over to BL-3011, which was PARKED at 20:58:14 when its push kept being refused.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2970'
```

- Expected: costUsd at most 3 times the median (5.40).
- Actual: claims=3, costUsd=5.602, outcome=requeued

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
