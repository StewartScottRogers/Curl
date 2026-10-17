---
id: BL-2582
title: Fix AF-0245: BL-2394 cost $9.53, 4.0 times the $2.38 median task cost: three claims, a 90-turn final run and a conflict-resolve run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-17
completed:
---
# BL-2582 — Fix AF-0245: BL-2394 cost $9.53, 4.0 times the $2.38 median task cost: three claims, a 90-turn final run and a conflict-resolve run

## Goal

What drove the past run's cost that the audit office reported as AF-0245 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0245 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0245-bl-2394-cost-9-53-4-0-times-the-2-38-median-task-c.md`.

Location: `logs/BL-2394-20261016-081911-L2.jsonl`

Location: `logs/BL-2394-20261016-081911-L2.jsonl`

Median costUsd over 254 tasks = 2.3824; threshold 3x = 7.15; only BL-2394 (9.5261) exceeds it. The 5 costliest: BL-2394 $9.53 (3 claims), BL-2324 $6.78, BL-2495 $6.77, BL-2318 $6.29 (2 claims), BL-2443 $6.10 (2 claims). BL-2394 runs: 20261015-200601-L2 $1.47 / 19 turns (requeued, split), 20261016-043300-L2 $1.91 / 23 turns (requeued, waits on BL-2479), 20261016-081911-L2 $5.66 / 90 turns (done), 20261016-081911-L2-resolve $0.48 / 9 turns (rebase conflict). What drove it: $3.38 on two runs that ended in requeues, and a long 90-turn final run that also hit a denied 'git stash list' permission at its format step (DarkFactory-20261016-081911-L2.log:162).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-15 -LogRoot Z:\repos\Curl.audit\20261016-162347\logs -CiRunsJson Z:\repos\Curl.audit\20261016-162347\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5
```

- Expected: No task over 3 times the median costUsd (7.15).
- Actual: BL-2394 claims 3, minutes 24.6, costUsd 9.5261.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
