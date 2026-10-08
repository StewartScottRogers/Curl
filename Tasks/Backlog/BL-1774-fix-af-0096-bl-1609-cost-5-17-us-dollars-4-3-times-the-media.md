---
id: BL-1774
title: Fix AF-0096: BL-1609 cost 5.17 US dollars, 4.3 times the median, over five runs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1774 — Fix AF-0096: BL-1609 cost 5.17 US dollars, 4.3 times the median, over five runs

## Goal

The defect the audit office reported as AF-0096 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0096 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0096-bl-1609-cost-5-17-us-dollars-4-3-times-the-median.md`.

Location: `logs/DarkFactory-20261007-201432-L1.log`

Location: `logs/DarkFactory-20261007-201432-L1.log`

Five runs: L4 $0.41, L8 $1.15, L6 $2.03 (opus $1.39 + one Sonnet sub-agent $0.64), L9 $0.41, L1 $1.18. Four of the five ended in a requeue; see the BL-1609 redone-work finding.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1609' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1609 claims 5 costUsd 5.1715

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
