---
id: BL-3460
title: Fix AF-0413: BL-3323 cost $2.71 (6.2x the median): a 47-minute Sonnet run requeued at the end because BL-3316 held Finish.cs, then an Opus retry
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-24
completed: 2026-10-24
---
# BL-3460 — Fix AF-0413: BL-3323 cost $2.71 (6.2x the median): a 47-minute Sonnet run requeued at the end because BL-3316 held Finish.cs, then an Opus retry

## Goal

What drove the past run's cost that the audit office reported as AF-0413 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0413 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0413-bl-3323-cost-2-71-6-2x-the-median-a-47-minute-sonn.md`.

Location: `logs/BL-3323-20261023-035126-L7.jsonl`

Location: `logs/BL-3323-20261023-035126-L7.jsonl`

Run 1 (L7, sonnet, 43 turns, 47.1 min, $0.98) passed its tests repeatedly (2405/2405, 2406/2406) and then requeued at 07:25:15: 'Finish.cs is held by BL-3316 (Doing)'. Its work was stashed. Run 2 (L6 11:08:15, opus retry, 34 turns, 17 min, $1.74) applied stash b0637982 and finished at 11:27:47. Cost drivers: a file overlap discovered only at integration, and the retry being raised to Opus for work that was already done.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-*-L*.log -Pattern 'BL-3323 (claim|end|stash|REQUEUE|DONE)'
```

- Expected: no task over 3 times the median costUsd
- Actual: BL-3323 costUsd 2.7136, claims 2; REQUEUE 'Finish.cs is held by BL-3316', then an opus retry

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause (from the task Context): run 1 (Sonnet, $0.98) finished its work and passed its tests, then requeued because Finish.cs was held by BL-3316 in Doing; run 2 was a retry, which DarkFactory/ModelChoice.ps1 raises to Opus by design (BL-1705), $1.74 to apply the stash and finish. Total $2.71 against a $0.44 median.
- Recorded as unavoidable here: lane rule 3 already tells a run to check other lanes' Doing touches on the shared branch when it needs a file outside its touches, and the overlap was a one-off between two tasks whose touches were set when filed. The Opus-on-retry rule is deliberate. No change to RunDarkFactory.ps1 is warranted. Build clean; fast tests green.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-24: Doing -> Done. Cost cause named (late file overlap plus by-design Opus retry), recorded unavoidable; build and fast tests green
