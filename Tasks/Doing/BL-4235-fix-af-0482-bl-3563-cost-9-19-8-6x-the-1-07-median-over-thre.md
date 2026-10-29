---
id: BL-4235
title: Fix AF-0482: BL-3563 cost $9.19 (8.6x the $1.07 median) over three opus runs, the last stopped at the 80-turn cap
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-29
completed:
---
# BL-4235 — Fix AF-0482: BL-3563 cost $9.19 (8.6x the $1.07 median) over three opus runs, the last stopped at the 80-turn cap

## Goal

What drove the past run's cost that the audit office reported as AF-0482 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0482 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0482-bl-3563-cost-9-19-8-6x-the-1-07-median-over-three.md`.

Location: `logs/BL-3563-20261028-052757-L1.jsonl`

Location: `logs/BL-3563-20261028-052757-L1.jsonl`

Runs: BL-3563-20261026-063535-L5 (17 turns, 7.7 min), BL-3563-20261026-151632-L5 (62 turns, 33.7 min) and BL-3563-20261028-052757-L1 (81 turns, 53.3 min, stopped at the 80-turn cap; DarkFactory-20261028-052757-L1.log:53). Cost was driven by re-reading the task's scope on every claim and by a final run that hit the cap before the task was split. Two of the three runs ended on dependencies found mid-run. Deferred and split into BL-3976 to BL-3978.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-3563'
```

- Expected: No task over 3x the median costUsd and over 5 USD.
- Actual: BL-3563 costUsd 9.1885, claims 3, outcome requeued.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
