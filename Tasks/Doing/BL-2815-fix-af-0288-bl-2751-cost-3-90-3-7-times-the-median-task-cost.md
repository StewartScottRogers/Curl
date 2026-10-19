---
id: BL-2815
title: Fix AF-0288: BL-2751 cost $3.90, 3.7 times the median task cost of $1.04 (87 turns, 27 edits)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-19
completed:
---
# BL-2815 — Fix AF-0288: BL-2751 cost $3.90, 3.7 times the median task cost of $1.04 (87 turns, 27 edits)

## Goal

What drove the past run's cost that the audit office reported as AF-0288 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0288 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0288-bl-2751-cost-3-90-3-7-times-the-median-task-cost-o.md`.

Location: `logs/BL-2751-20261018-103803-L3.jsonl`

Location: `logs/BL-2751-20261018-103803-L3.jsonl`

Measure-FactoryProcess.ps1 -Since 2026-10-18: BL-2751 claims=1 minutes=35.73 costUsd=3.8954 outcome=done; threshold 3.1176. The run result shows num_turns=87, 7411527 cache-read tokens, 57116 output tokens, 85 tool calls (41 Bash), model opus. Lane log DarkFactory-20261018-103803-L3.log runs from 13:21:03 claim ('RtspProtocolHandler makes any libcurl RTSP request from per-perform s...') to 13:56:47 DONE, with 27 edits, 5 writes, 4 quality runs and 2 renums. The task was the prerequisite BL-2704 was requeued to wait on at 13:20:45. The cost came from editing a broad handler surface (27 edits) and from 4 quality cycles.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261018-190531\logs -CiRunsJson Z:\repos\Curl.audit\20261018-190531\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2751'
```

- Expected: costUsd at most 3 times the median (3.1176).
- Actual: BL-2751 costUsd 3.8954.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
