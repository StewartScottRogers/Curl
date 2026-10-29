---
id: BL-4237
title: Fix AF-0484: BL-3659 cost $5.48 (5.1x median): its first opus run hit the 80-turn cap
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-29
completed:
---
# BL-4237 — Fix AF-0484: BL-3659 cost $5.48 (5.1x median): its first opus run hit the 80-turn cap

## Goal

What drove the past run's cost that the audit office reported as AF-0484 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0484 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0484-bl-3659-cost-5-48-5-1x-median-its-first-opus-run-h.md`.

Location: `logs/BL-3659-20261026-151632-L4.jsonl`

Location: `logs/BL-3659-20261026-151632-L4.jsonl`

Run L4 on 2026-10-26, 18:15:17 to 18:52:39: 81 turns in 37.3 min, requeued on the 80-turn cap (DarkFactory-20261026-151632-L4.log:148). The retry on L6, 2026-10-28 06:07:53 to 06:35:54, took 46 turns and finished Done. The cap was the driver.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-3659'
```

- Expected: No task over 3x the median costUsd and over 5 USD.
- Actual: BL-3659 costUsd 5.4816, claims 2, outcome done.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
