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
completed: 2026-10-22
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

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-22: Cause: BL-3079 (touches DarkFactory/TaskBudget.ps1 and RunDarkFactory.ps1) was claimed, and in its first run (11 turns, $0.19) the lane found TaskBudget.ps1 was needed and BL-3090 in Doing already held it, so it requeued (BL-3079 Notes record this). The second claim (20 turns, $0.74) did the real work and finished. The first run stopped inside the first dozen turns, as the lane rules ask, and left no code stashed; the only way to avoid it was to know at claim time that the file was needed, but the original touches did not list it: the need showed only once the code was read, and BL-3090 started in the meantime. Unavoidable: $0.19 of reading is the cheapest the discovery could cost, and the cost was within a factor of the expected $0.81 (0.93 total). No change made; the finding closes at the first audit after Done (task-done-historical). The build and fast tests were not run: no file changed.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-22: Doing -> Done. AF-0361 cost named: a file overlap found in a cheap first run, unavoidable, no waste to fix
