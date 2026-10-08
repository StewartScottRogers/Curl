---
id: BL-1784
title: Fix AF-0106: BL-1619 cost 3.79 US dollars, 3.2 times the median; a 66-turn run on lane 2 was parked on the flaky cookie test
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1784 — Fix AF-0106: BL-1619 cost 3.79 US dollars, 3.2 times the median; a 66-turn run on lane 2 was parked on the flaky cookie test

## Goal

The defect the audit office reported as AF-0106 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0106 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0106-bl-1619-cost-3-79-us-dollars-3-2-times-the-median.md`.

Location: `logs/DarkFactory-20261007-111121-L2.log`

Location: `logs/DarkFactory-20261007-111121-L2.log`

L2 run: 24.7 min, 66 turns, $3.32, then PARKED at 15:49:29 (EveryMember_ManyConcurrentCallers cookie test). L4 finished it in 2.4 min for $0.46 (DONE 16:11:03).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1619' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1619 claims 2 costUsd 3.7869

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
