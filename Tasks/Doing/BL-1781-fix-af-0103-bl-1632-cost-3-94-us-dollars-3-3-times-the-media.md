---
id: BL-1781
title: Fix AF-0103: BL-1632 cost 3.94 US dollars, 3.3 times the median; a 73-turn run on lane 6 could not integrate
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1781 — Fix AF-0103: BL-1632 cost 3.94 US dollars, 3.3 times the median; a 73-turn run on lane 6 could not integrate

## Goal

The defect the audit office reported as AF-0103 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0103 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0103-bl-1632-cost-3-94-us-dollars-3-3-times-the-median.md`.

Location: `logs/DarkFactory-20261007-111121-L6.log`

Location: `logs/DarkFactory-20261007-111121-L6.log`

L6 run: 16.1 min, 73 turns, $3.00, then PARKED at 15:28:19 (fast tests failed twice: Curl.Protocol.Smtp.UnitTests, then no test named). L2 redid it in 7.5 min for $0.94 (DONE 16:21:24).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1632' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1632 claims 2 costUsd 3.9408

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
