---
id: BL-2949
title: Fix AF-0311: BL-2788 cost $4.61, 3.3 times the median, and the factory never finished it
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskStash.ps1, DarkFactory/Requeue.ps1]
requirement: none
created: 2026-10-21
completed:
---
# BL-2949 — Fix AF-0311: BL-2788 cost $4.61, 3.3 times the median, and the factory never finished it

## Goal

What drove the past run's cost that the audit office reported as AF-0311 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0311 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0311-bl-2788-cost-4-61-3-3-times-the-median-and-the-fac.md`.

Location: `logs/BL-2788-20261018-192454-L3.jsonl`

Location: `logs/BL-2788-20261018-192454-L3.jsonl`

Run 1: $4.07, 79 turns, 19 min, 8.77M cache-read tokens, 45 Bash and 23 Edit calls. Its passing work was stashed when a dependency (BL-2789) turned up mid-run. Run 2 (BL-2788-20261019-061607-L1.jsonl): $0.53, 8 turns. The stash did not apply cleanly and the task was split. The lanes spent $4.61 and never finished the task; an interactive session completed it outside the cost cap. What drove the cost: one long run whose result was not integrated, then a split.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2788'
```

- Expected: costUsd at most 4.2465 (3 times the median).
- Actual: BL-2788 claims 4, minutes 0.27, costUsd 4.6076, blocked

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
