---
id: BL-3088
title: Fix AF-0335: BL-2924 cost $5.56, 3.1 times the median, in one 83-turn run plus a rebase-conflict resolve
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Integrate.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3088 — Fix AF-0335: BL-2924 cost $5.56, 3.1 times the median, in one 83-turn run plus a rebase-conflict resolve

## Goal

What drove the past run's cost that the audit office reported as AF-0335 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0335 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0335-bl-2924-cost-5-56-3-1-times-the-median-in-one-83-t.md`.

Location: `logs/DarkFactory-20261021-064209-L4.log:216`

Location: `logs/DarkFactory-20261021-064209-L4.log:216`

BL-2924-20261021-064209-L4: 83 turns, 29.1 min, $5.06, 10.9M cache-read tokens, 42 Bash calls and 74 dotnet build/test mentions. A long build-and-test loop for one unit-program port (unit1658). Then BL-2924-20261021-064209-L4-resolve: 12 turns, $0.51 for a rebase conflict. DONE 10:28:11. The cost comes from turn count multiplied by context size, not from retries.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2924'
```

- Expected: costUsd at most 3 times the median (5.40).
- Actual: claims=1, costUsd=5.5624, outcome=done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause: BL-2924 was one legitimate unit-program port (unit1658) that needed a long build-and-test loop; cost is turn count (83) times context size (10.9M cache-read tokens), not retries, plus a 12-turn rebase-conflict resolve ($0.51) caused by other lanes landing concurrently. No avoidable waste in the factory scripts: the run is already bounded by the per-task cost cap and the lane rules on batching builds. Recorded as unavoidable; no code change. Build and fast tests green.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-22: Doing -> Done. Cost cause named (one long legitimate port run plus a rebase resolve), recorded as unavoidable
