---
id: BL-1281
title: Fix AF-0025: BL-1121 claimed 5 times, BL-892 4 times and BL-907 3 times
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-03
completed:
---
# BL-1281 — Fix AF-0025: BL-1121 claimed 5 times, BL-892 4 times and BL-907 3 times

## Goal

The defect the audit office reported as AF-0025 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0025 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0025-bl-1121-claimed-5-times-bl-892-4-times-and-bl-907.md`.

Location: `logs/BL-1121`

Location: `logs/BL-1121`

Measure-FactoryProcess.ps1 reports claims=5 for BL-1121 (26.82 min, done), claims=4 for BL-892 (12.85 min) and claims=3 for BL-907 (20.23 min). The rule threshold is 3 or more claims. In total 10 tasks were claimed more than once and requeues=11. The reasons were not investigated.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-30 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object claims -ge 3 | Select-Object id,claims
```

- Expected: No task claimed 3 or more times.
- Actual: BL-1121 5, BL-892 4, BL-907 3

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
