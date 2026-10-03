---
id: BL-1377
title: Fix AF-0033: BL-1284, BL-1320 and BL-1287 cost over 3x the median task cost
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-03
completed:
---
# BL-1377 — Fix AF-0033: BL-1284, BL-1320 and BL-1287 cost over 3x the median task cost

## Goal

The defect the audit office reported as AF-0033 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0033 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0033-bl-1284-bl-1320-and-bl-1287-cost-over-3x-the-media.md`.

Location: `logs/BL-1284-20261003-061205-L6.jsonl`

Location: `logs/BL-1284-20261003-061205-L6.jsonl`

Median costUsd about 1.32 (bar about 3.96). BL-1284 5.39 USD (29 min), BL-1320 4.26 (25 min), BL-1287 4.16 (102 min). Next: BL-1355 2.93, BL-1312 2.83. I did not examine what drove the cost.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5 id,costUsd
```

- Expected: No task over 3x the median.
- Actual: BL-1284 5.39, BL-1320 4.26, BL-1287 4.16

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
