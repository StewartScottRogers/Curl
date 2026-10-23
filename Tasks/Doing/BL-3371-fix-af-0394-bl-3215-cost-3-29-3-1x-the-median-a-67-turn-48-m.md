---
id: BL-3371
title: Fix AF-0394: BL-3215 cost $3.29 (3.1x the median): a 67-turn, 48-minute Opus run plus a rebase-conflict resolve run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-23
completed:
---
# BL-3371 — Fix AF-0394: BL-3215 cost $3.29 (3.1x the median): a 67-turn, 48-minute Opus run plus a rebase-conflict resolve run

## Goal

What drove the past run's cost that the audit office reported as AF-0394 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0394 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0394-bl-3215-cost-3-29-3-1x-the-median-a-67-turn-48-min.md`.

Location: `logs/BL-3215-20261022-192710-L2.jsonl`

Location: `logs/BL-3215-20261022-192710-L2.jsonl`

Main run: Opus, 67 turns, 47.7 min, $3.07 (23:27:19 to 00:15:02). The trace shows repeated partial test passes (5/12, 6/12), then a 29-minute gap between test runs (23:40:18 to 00:09:28). Resolve run: 9 turns, $0.22. Task: reject a revoked server certificate on Windows with exit 35.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261022-192710-L2.log -Pattern 'BL-3215 (claim|test|end|resolve)'
```

- Expected: costUsd at most 3x the median ($3.19).
- Actual: tasks[BL-3215].costUsd = 3.2925

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
