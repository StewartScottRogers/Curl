---
id: BL-1779
title: Fix AF-0101: BL-1627 cost 4.32 US dollars, 3.6 times the median; a 70-turn run on lane 7 was parked on the flaky cookie test
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1779 — Fix AF-0101: BL-1627 cost 4.32 US dollars, 3.6 times the median; a 70-turn run on lane 7 was parked on the flaky cookie test

## Goal

The defect the audit office reported as AF-0101 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0101 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0101-bl-1627-cost-4-32-us-dollars-3-6-times-the-median.md`.

Location: `logs/DarkFactory-20261007-111121-L7.log`

Location: `logs/DarkFactory-20261007-111121-L7.log`

L7 run: 13.5 min, 70 turns, $3.56, then PARKED at 16:05:48 (fast tests failed twice: Curl.Http2.UnitTests, then the EveryMember cookie test). L3 redid it at 16:41:08 in 6.6 min for $0.76.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1627' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1627 claims 2 costUsd 4.3163

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
