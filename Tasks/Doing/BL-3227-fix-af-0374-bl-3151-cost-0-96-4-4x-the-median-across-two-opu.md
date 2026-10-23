---
id: BL-3227
title: Fix AF-0374: BL-3151 cost $0.96 (4.4x the median) across two Opus runs that each found the same unmet precondition and requeued
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-23
completed:
---
# BL-3227 — Fix AF-0374: BL-3151 cost $0.96 (4.4x the median) across two Opus runs that each found the same unmet precondition and requeued

## Goal

What drove the past run's cost that the audit office reported as AF-0374 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0374 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0374-bl-3151-cost-0-96-4-4x-the-median-across-two-opus.md`.

Location: `logs/BL-3151-20261022-164804-L1.jsonl`

Location: `logs/BL-3151-20261022-164804-L1.jsonl`

BL-3151 costUsd=0.9619, against a bar of 0.662, and the task is not done. Run 1: 26 turns, $0.643, 980,161 cache-read tokens, 8,975 output tokens. Its time went on probing live pages and reporting that download/ and ai-help/ still return 404 until the shift-end merge. Run 2 ('retry after a run that came back'): 12 turns, $0.319, the same conclusion. The cause is the redone-work finding for BL-3151: a claim was allowed before the merge it depends on had landed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-190247\logs -CiRunsJson Z:\repos\Curl.audit\20261022-190247\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3151'
```

- Expected: costUsd at most 3 times the median (0.662).
- Actual: BL-3151 claims=2 minutes=1.35 costUsd=0.9619 outcome=requeued

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
