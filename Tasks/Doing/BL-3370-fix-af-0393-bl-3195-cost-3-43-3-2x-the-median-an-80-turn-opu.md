---
id: BL-3370
title: Fix AF-0393: BL-3195 cost $3.43 (3.2x the median): an 80-turn Opus run plus two rebase-conflict resolve runs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-23
completed:
---
# BL-3370 — Fix AF-0393: BL-3195 cost $3.43 (3.2x the median): an 80-turn Opus run plus two rebase-conflict resolve runs

## Goal

What drove the past run's cost that the audit office reported as AF-0393 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0393 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0393-bl-3195-cost-3-43-3-2x-the-median-an-80-turn-opus.md`.

Location: `logs/BL-3195-20261022-192710-L2.jsonl`

Location: `logs/BL-3195-20261022-192710-L2.jsonl`

Main run: Opus, 80 turns, 33.3 min, $3.17 (21:25:08 to 21:58:29). Two resolve runs followed, at 21:58:32 (12 turns) and 22:04:45 (11 turns), about $0.71 together by the jsonl results. Each one rebuilt and retested after conflicts with lanes integrating Engine work at the same time.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261022-192710-L2.log -Pattern 'BL-3195 (claim|end|resolve)'
```

- Expected: costUsd at most 3x the median ($3.19).
- Actual: tasks[BL-3195].costUsd = 3.4325

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
