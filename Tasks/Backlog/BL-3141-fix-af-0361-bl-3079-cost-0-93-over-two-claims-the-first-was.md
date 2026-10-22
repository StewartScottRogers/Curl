---
id: BL-3141
title: Fix AF-0361: BL-3079 cost $0.93 over two claims; the first was requeued because DarkFactory/TaskBudget.ps1 was held by another task
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3141 — Fix AF-0361: BL-3079 cost $0.93 over two claims; the first was requeued because DarkFactory/TaskBudget.ps1 was held by another task

## Goal

What drove the past run's cost that the audit office reported as AF-0361 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0361 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0361-bl-3079-cost-0-93-over-two-claims-the-first-was-re.md`.

Location: `logs/BL-3079-20261022-080746-L1.jsonl`

Location: `logs/BL-3079-20261022-080746-L1.jsonl`

First run BL-3079-20261022-062551-L3.jsonl: 11 turns, cost 0.1933, then 'REQUEUE ... DarkFactory/TaskBudget.ps1 is held by B~' at 06:51:49 on L3. Second run BL-3079-20261022-080746-L1.jsonl: 20 turns, cost 0.7392. The file overlap forced a second run.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3079'
```

- Expected: costUsd at most 0.8106
- Actual: claims 2, minutes 5.45, costUsd 0.9325, done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
