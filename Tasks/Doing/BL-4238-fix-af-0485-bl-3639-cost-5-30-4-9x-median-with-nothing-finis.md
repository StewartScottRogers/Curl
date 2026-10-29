---
id: BL-4238
title: Fix AF-0485: BL-3639 cost $5.30 (4.9x median) with nothing finished: a requeue on held files, then 81 turns in 14 min at the cap
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-29
completed:
---
# BL-4238 — Fix AF-0485: BL-3639 cost $5.30 (4.9x median) with nothing finished: a requeue on held files, then 81 turns in 14 min at the cap

## Goal

What drove the past run's cost that the audit office reported as AF-0485 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0485 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0485-bl-3639-cost-5-30-4-9x-median-with-nothing-finishe.md`.

Location: `logs/BL-3639-20261028-052757-L9.jsonl`

Location: `logs/BL-3639-20261028-052757-L9.jsonl`

Run L6 on 2026-10-26: 27 turns, 14.4 min, requeued waiting on files held by BL-3570 and BL-3893 (DarkFactory-20261026-151632-L6.log:104). Run L9 on 2026-10-28, 06:06:02 to 06:20:14: 81 turns in 14.2 min, stopped at the 80-turn cap (DarkFactory-20261028-052757-L9.log:20). The task was then Deferred and split. All $5.30 was spent with no task Done.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-3639'
```

- Expected: No task over 3x the median costUsd and over 5 USD.
- Actual: BL-3639 costUsd 5.304, claims 2, outcome requeued.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
