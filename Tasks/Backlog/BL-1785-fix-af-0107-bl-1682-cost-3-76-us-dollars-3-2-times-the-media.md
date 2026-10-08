---
id: BL-1785
title: Fix AF-0107: BL-1682 cost 3.76 US dollars, 3.2 times the median, over three runs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1785 — Fix AF-0107: BL-1682 cost 3.76 US dollars, 3.2 times the median, over three runs

## Goal

The defect the audit office reported as AF-0107 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0107 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0107-bl-1682-cost-3-76-us-dollars-3-2-times-the-median.md`.

Location: `logs/DarkFactory-20261007-201432-L9.log`

Location: `logs/DarkFactory-20261007-201432-L9.log`

Runs: L9 $1.79 (48 turns, measured but not fixed), L2 $0.73 (waits on BL-1715), L8 $1.24 (done). See the BL-1682 redone-work finding.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1682' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1682 claims 3 costUsd 3.7582

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
