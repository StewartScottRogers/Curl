---
id: BL-2947
title: Fix AF-0309: BL-2787 cost $4.85, 3.4 times the median task cost of $1.42
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Prompts.ps1, DarkFactory/TaskBudget.ps1]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2947 — Fix AF-0309: BL-2787 cost $4.85, 3.4 times the median task cost of $1.42

## Goal

What drove the past run's cost that the audit office reported as AF-0309 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0309 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0309-bl-2787-cost-4-85-3-4-times-the-median-task-cost-o.md`.

Location: `logs/BL-2787-20261018-192454-L1.jsonl`

Location: `logs/BL-2787-20261018-192454-L1.jsonl`

Median costUsd over the 100 costed tasks is 1.4155, so the 3x bar is 4.2465. The 5 costliest tasks: BL-2787 4.8482, BL-2743 4.7472, BL-2788 4.6076, BL-2724 4.4497, BL-2726 4.1982. BL-2787 was one run: 82 turns in 24.6 min (claim 03:33:38, end 03:58:18 in DarkFactory-20261018-192454-L1.log), 10.83M cache-read input tokens (about 132K context re-read per turn), 47K output. Tools: 49 Bash calls (9 of them dotnet build, 1 dotnet test), 27 Edit, 2 Read. What drove the cost: a long single session that rebuilt 9 times while the whole context was re-read every turn. No retries.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5
```

- Expected: No task over 3 times the median costUsd (4.2465).
- Actual: BL-2787 4.8482, BL-2743 4.7472, BL-2788 4.6076, BL-2724 4.4497, BL-2726 4.1982

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause: one 82-turn session with 9 builds and 27 one-line Edits, each turn re-reading ~132K of context; no retries. Avoidable: edit-build churn. Fix: AF-0309 clause added to the unattended-run rules in DarkFactory/Prompts.ps1 (both copies): make all edits before building, batch edits per file, build once per change.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-21: Doing -> Done. Cause named; prompt rule added
