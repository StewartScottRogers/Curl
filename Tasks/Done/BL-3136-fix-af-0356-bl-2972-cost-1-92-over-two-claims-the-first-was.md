---
id: BL-3136
title: Fix AF-0356: BL-2972 cost $1.92 over two claims; the first was requeued because it needed a file outside its touches
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3136 — Fix AF-0356: BL-2972 cost $1.92 over two claims; the first was requeued because it needed a file outside its touches

## Goal

What drove the past run's cost that the audit office reported as AF-0356 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0356 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0356-bl-2972-cost-1-92-over-two-claims-the-first-was-re.md`.

Location: `logs/BL-2972-20261022-024438-L3.jsonl`

Location: `logs/BL-2972-20261022-024438-L3.jsonl`

First run (L3, 02:45:42-02:48:18): 17 turns, cost 0.5947, then 'REQUEUE ... Needs Curl.Networking.UnitLibrary/DohTr~' at 02:51:41. Second run (BL-2972-20261022-042722-L1.jsonl, 04:27:54-04:37:46, 'retry after a run that came back'): 43 turns, cost 1.3260. Its touches were too narrow, which cost about $0.59 of discarded work and a 2-hour delay.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-112013\logs -CiRunsJson Z:\repos\Curl.audit\20261022-112013\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2972'; Select-String -Path Z:\repos\Curl.audit\20261022-112013\logs\DarkFactory-20261022-*-L*.log -Pattern 'BL-2972 +(claim|REQUEUE|end)'
```

- Expected: costUsd at most 0.8106
- Actual: claims 2, minutes 11.12, costUsd 1.9207, done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause: BL-2972's first claim (L3) had touches too narrow; it needed Curl.Networking.UnitLibrary/DohTr~ and requeued after 17 turns ($0.59 discarded, 2-hour delay). Already fixed: the lane prompt (DarkFactory/Prompts.ps1, rule 3) now tells a run to read other Doing tasks' touches on the shared branch, add the file to its own touches and carry on, requeueing only on a real overlap. No further waste to remove; the rest ($1.33 second claim) was the task's real work.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-22: Doing -> Done. Cost cause named (touches too narrow); already fixed by lane rule 3 widening touches instead of requeueing
