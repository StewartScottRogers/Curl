---
id: BL-1375
title: Fix AF-0034: BL-1360 run (lane 7) ends without DONE or BLOCKED after 87 minutes; BL-1325 blocked after 120 minutes
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-03
completed:
---
# BL-1375 — Fix AF-0034: BL-1360 run (lane 7) ends without DONE or BLOCKED after 87 minutes; BL-1325 blocked after 120 minutes

## Goal

The defect the audit office reported as AF-0034 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0034 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0034-bl-1360-run-lane-7-ends-without-done-or-blocked-af.md`.

Location: `logs/DarkFactory-20261003-061205-L7.log`

Location: `logs/DarkFactory-20261003-061205-L7.log`

Tool outcome 'open' for BL-1360 (87.72 min, cost 0); L7's last line at 12:20:35 is a quality step. BL-1325 outcome 'blocked' (120.57 min, cost 0). I did not find the cause.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object outcome -ne 'done' | Select-Object id,outcome,minutes
```

- Expected: No open runs.
- Actual: BL-1360 open, BL-1325 blocked

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
